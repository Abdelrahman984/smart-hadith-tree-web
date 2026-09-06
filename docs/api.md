# REST API Reference

The backend API is hosted at `http://localhost:5147/api`.

## 1. Search

### `GET /api/hadith/search?q={query}`
Searches for hadiths by text, book name, or narrator name.

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "bookName": "صحيح البخاري",
    "hadithNumber": 1,
    "chapter": "بدء الوحي",
    "matnSnippet": "إنما الأعمال بالنيات..."
  }
]
```

### `GET /api/narrators/search?q={query}`
Searches for narrators by their full name or aliases.

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "fullName": "مالك بن أنس",
    "knownAs": "الإمام مالك",
    "generationTier": "كبار أتباع التابعين",
    "deathYearHijri": 179
  }
]
```

## 2. Isnad Tree (Graph Data)

### `GET /api/tree/{hadithId}`
Returns the full Isnad tree for a specific Hadith using a Recursive CTE query. 

**Response (200 OK):**
```json
{
  "hadithId": "guid",
  "bookName": "صحيح البخاري",
  "hadithNumber": 1,
  "matnArabic": "حدثنا الحميدي...",
  "nodes": [
    {
      "id": "guid (transmission_id)",
      "narratorId": "guid",
      "parentNodeId": "guid (parent_transmission_id)",
      "narratorName": "سفيان بن عيينة",
      "knownAs": "سفيان",
      "generationTier": "أتباع التابعين",
      "transmissionTerm": "حدثنا",
      "stepOrder": 2
    }
  ]
}
```

## 3. Narrator Details & AI

### `GET /api/narrators/{id}`
Returns detailed biographical info and all classical scholar evaluations.

### `GET /api/narrators/{id}/tooltip`
Returns a lightweight summary suitable for UI hover cards.

### `GET /api/narrators/{id}/ai-summary`
Invokes the Semantic Kernel to run a RAG prompt against the narrator's evaluations and returns a dynamic AI ruling.

**Response (200 OK):**
```json
{
  "summary": "بناءً على أقوال العلماء (ابن معين، أبو حاتم)، الراوي ثقة حافظ ومتقن لحديث الزهري."
}
```
