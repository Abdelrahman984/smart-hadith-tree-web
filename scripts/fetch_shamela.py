import os
import sys
import json
import time
import urllib.request
import urllib.parse
import urllib.error

sys.stdout.reconfigure(encoding='utf-8')

TOKEN_FILE = r"C:\Users\rafee\.gemini\antigravity\mcp_oauth_tokens.json"
BASE_URL = "https://shamela.link/mcp"
BASE_DATA_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "data", "shamela"))

BOOKS = [
    {"id": 1456, "slug": "musnad_tayalisi"},
    {"id": 9344, "slug": "musnad_shafii"},
    {"id": 8493, "slug": "musnad_humaydi"},
    {"id": 13122, "slug": "sunan_said_ibn_mansur"},
    {"id": 13159, "slug": "musnad_ishaq"},
    {"id": 12981, "slug": "musnad_bazzar"},
    {"id": 8361, "slug": "sunan_kubra_nasai"},
    {"id": 12520, "slug": "musnad_abi_yala"},
    {"id": 1446, "slug": "sahih_ibn_khuzaymah"},
    {"id": 18144, "slug": "mustakhraj_abi_awanah"},
    {"id": 537, "slug": "sahih_ibn_hibban"},
    {"id": 1733, "slug": "mujam_kabir_tabarani"},
    {"id": 28171, "slug": "mujam_awsat_tabarani"},
    {"id": 1734, "fallback_id": 13068, "slug": "mujam_saghir_tabarani"},
    {"id": 9771, "slug": "sunan_daraqutni"},
    {"id": 1424, "slug": "mustadrak_hakim"},
    {"id": 148486, "slug": "sunan_kubra_bayhaqi"},
    {"id": 10660, "slug": "shuab_iman_bayhaqi"},
]

def get_access_token():
    with open(TOKEN_FILE, "r", encoding="utf-8") as f:
        data = json.load(f)
    return data["https://shamela.link/mcp"]["token"]["access_token"]

def refresh_access_token():
    with open(TOKEN_FILE, "r", encoding="utf-8") as f:
        tokens_data = json.load(f)
    entry = tokens_data["https://shamela.link/mcp"]
    token_url = entry["token_url"]
    refresh_tok = entry["token"]["refresh_token"]
    client_id = entry["client_id"]

    post_data = urllib.parse.urlencode({
        "grant_type": "refresh_token",
        "refresh_token": refresh_tok,
        "client_id": client_id,
    }).encode("utf-8")

    req = urllib.request.Request(
        token_url,
        data=post_data,
        headers={"Content-Type": "application/x-www-form-urlencoded"}
    )
    with urllib.request.urlopen(req) as resp:
        res = json.loads(resp.read().decode("utf-8"))
        entry["token"]["access_token"] = res["access_token"]
        if "refresh_token" in res:
            entry["token"]["refresh_token"] = res["refresh_token"]
    with open(TOKEN_FILE, "w", encoding="utf-8") as f:
        json.dump(tokens_data, f, indent=2)
    print("Token refreshed successfully.")
    return entry["token"]["access_token"]

def call_mcp(tool_name, arguments, retries=4):
    token = get_access_token()
    for attempt in range(retries):
        try:
            req = urllib.request.Request(
                BASE_URL,
                data=json.dumps({
                    "jsonrpc": "2.0",
                    "id": int(time.time() * 1000),
                    "method": "tools/call",
                    "params": {"name": tool_name, "arguments": arguments}
                }).encode("utf-8"),
                headers={
                    "Content-Type": "application/json",
                    "Authorization": f"Bearer {token}",
                    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                    "Accept": "application/json, text/event-stream"
                }
            )
            with urllib.request.urlopen(req, timeout=45) as resp:
                body = resp.read().decode("utf-8")
                for line in body.splitlines():
                    if line.startswith("data:"):
                        payload = json.loads(line[5:].strip())
                        if "result" in payload:
                            for item in payload["result"].get("content", []):
                                if item.get("type") == "text":
                                    text = item["text"]
                                    if "BOOK_NOT_FOUND" in text:
                                        return {"error": "BOOK_NOT_FOUND", "raw": text}
                                    return json.loads(text)
                        elif "error" in payload:
                            raise Exception(f"MCP error: {payload['error']}")
                raise Exception("No data payload in SSE response")
        except urllib.error.HTTPError as e:
            if e.code == 401 and attempt < retries - 1:
                print("401 Unauthorized, refreshing token...")
                token = refresh_access_token()
                time.sleep(1)
                continue
            elif e.code in (429, 500, 502, 503, 504) and attempt < retries - 1:
                wait_time = (attempt + 1) * 3
                print(f"HTTP {e.code}, waiting {wait_time}s before retry...")
                time.sleep(wait_time)
                continue
            else:
                err_body = e.read().decode("utf-8", errors="replace")
                raise Exception(f"HTTP {e.code}: {err_body}")
        except Exception as e:
            if attempt < retries - 1:
                wait_time = (attempt + 1) * 2
                print(f"Error {e}, waiting {wait_time}s before retry...")
                time.sleep(wait_time)
                continue
            raise

def process_all_books():
    total_books = len(BOOKS)
    summary = []
    
    print(f"Starting fetch for {total_books} books to '{BASE_DATA_DIR}'...")
    os.makedirs(BASE_DATA_DIR, exist_ok=True)
    
    for book_idx, book_info in enumerate(BOOKS, start=1):
        book_id = book_info["id"]
        slug = book_info["slug"]
        print(f"\n[{book_idx}/{total_books}] Processing '{slug}' (ID: {book_id})...")
        
        book_dir = os.path.join(BASE_DATA_DIR, slug)
        os.makedirs(book_dir, exist_ok=True)
        
        # 1. Fetch TOC
        actual_book_id = book_id
        toc = call_mcp("shamela_get_toc", {"book_id": actual_book_id, "depth": 1, "response_format": "json"})
        if isinstance(toc, dict) and toc.get("error") == "BOOK_NOT_FOUND" and "fallback_id" in book_info:
            fallback = book_info["fallback_id"]
            print(f"   Book ID {book_id} not found, falling back to ID {fallback}...")
            actual_book_id = fallback
            toc = call_mcp("shamela_get_toc", {"book_id": actual_book_id, "depth": 1, "response_format": "json"})
        
        titles = toc.get("titles", [])
        if not titles:
            print(f"   WARNING: No titles in TOC for {slug} (ID: {actual_book_id})")
            summary.append({"slug": slug, "book_id": actual_book_id, "chapters_fetched": 0, "status": "no_titles"})
            continue
        
        # We fetch up to 5 chapters
        chapters_to_fetch = titles[:5]
        print(f"   Found {len(titles)} titles. Fetching first {len(chapters_to_fetch)} chapters...")
        
        index_entries = []
        for ch_idx, title in enumerate(chapters_to_fetch, start=1):
            title_id = title["title_id"]
            title_text = title["title_text"]
            print(f"     Chapter {ch_idx}: [Title ID: {title_id}] '{title_text}'...")
            
            section_data = call_mcp("shamela_get_book_section", {
                "book_id": actual_book_id,
                "title_id": title_id,
                "response_format": "json"
            })
            
            # Save section JSON
            ch_filename = f"{ch_idx}.json"
            ch_path = os.path.join(book_dir, ch_filename)
            with open(ch_path, "w", encoding="utf-8") as f:
                json.dump(section_data, f, ensure_ascii=False, indent=2)
            
            pages = section_data.get("pages", [])
            entry = {
                "chapter": ch_idx,
                "file": ch_filename,
                "title_id": section_data.get("title_id", title_id),
                "name_ar": section_data.get("title_text", title_text),
                "name_en": "",
                "count": len(pages),
                "start_page_id": section_data.get("start_page_id", title.get("page_id", 0)),
                "end_page_id": section_data.get("end_page_id", 0),
                "total_pages_in_section": section_data.get("total_pages_in_section", len(pages))
            }
            index_entries.append(entry)
            time.sleep(0.3)
        
        # Save index.json
        index_path = os.path.join(book_dir, "index.json")
        with open(index_path, "w", encoding="utf-8") as f:
            json.dump(index_entries, f, ensure_ascii=False, indent=2)
        
        summary.append({
            "slug": slug,
            "book_id": actual_book_id,
            "chapters_fetched": len(index_entries),
            "status": "success"
        })
        print(f"   Finished '{slug}' ({len(index_entries)} chapters saved).")
        time.sleep(0.5)

    print("\n===============================")
    print("ALL BOOKS PROCESSED SUCCESSFULLY!")
    print("===============================")
    summary_path = os.path.join(BASE_DATA_DIR, "fetch_summary.json")
    with open(summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print(f"Summary written to {summary_path}")

if __name__ == "__main__":
    process_all_books()
