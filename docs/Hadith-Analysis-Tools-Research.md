# **Computational Frameworks for Hadith Takhreej and 'Ilal Analysis: A Comprehensive Open-Source Landscape Report**

The development of computational systems for *takhreej* (hadith extraction and referencing) and *'ilal al-hadith* (hidden defect analysis) represents a highly specialized intersection of classical Islamic epistemology and modern digital humanities. The objective of this report is to exhaustively evaluate the existing open-source landscape of formalized *mustalah al-hadith* (hadith terminology and rules) engines, expert-labeled datasets, structured *jarh wa ta'dil* (narrator criticism) repositories, and algorithmic components.  
Given the advanced baseline of the target architecture—which already features a proprietary parser achieving \~97% precision on *isnad* segmentation across 31 classical texts and a foundational registry of 23,500 narrators—this analysis deliberately bypasses elementary hadith digitization projects. Instead, it focuses exclusively on locating machine-readable rule systems, graph databases modeling classical scholarly criticism, mathematical *matn* (text) alignment for detecting *ziyadat al-thiqa* (additions by reliable narrators), and rigorously labeled ground-truth datasets that can directly replace heuristic "engineering guesses" with classical rigor.

## **Executive Summary of Candidate Repositories**

The following evaluation matrix categorizes the most viable open-source repositories based on their utility, licensing, and immediate integrability into an advanced Python-based hadith analytics stack.

| Candidate Project / Repository | Resource Typology | Explicit License | Verdict & Primary Utility |
| :---- | :---- | :---- | :---- |
| **1\. alizahidraja/isnad** | Python AI Governance / Rule Engine | Apache-2.0 (Code), CC-BY-4.0 (Docs) | **Use directly.** Ready-made, highly evaluated Python rule engine for *mustalah* weakest-link algorithms and decision matrices. |
| **2\. R3GENESI5/Itqan** | Relational Graph & Rijal Dataset | MIT (Code), Public Domain (Data) | **Use directly.** Highest priority for massive, structured *jarh wa ta'dil* data and classical rule ontologies in JSON. |
| **3\. OmarShafie/hadith (KASHAF)** | Algorithmic Component (Matn Alignment) | GPL-3.0 | **Use directly (with license caveats).** Critical mathematical logic for computing *ziyadat al-thiqa* and *shudhudh* via sequence alignment. |
| **4\. emadjumaah/hadith-kg** | Dataset (Knowledge Graph) | CC-BY-4.0 | **Use directly.** Massive ground-truth dataset of 127,863 expert grading statements. |
| **5\. A-Kamran/SemanticHadith-V2** | Formal Ontology / OWL | CC0-1.0 (Public Domain) | **Use as reference.** Provides a peer-reviewed schema for structuring *mustalah* data in RDF/graph formats. |
| **6\. AhmedElTabarani/dorar-hadith-api** | Live API Wrapper (Proxy) | Unspecified (Open Source) | **Use as reference.** Helpful for dynamically validating algorithm outputs against Dorar.net's expert labels. |
| **7\. TaghreedT/NAH-Corpus** | Dataset (Non-Authentic Hadiths) | Unspecified / Academic | **Use as reference.** Vital for adversarial stress-testing of *'ilal* detection engines on fabricated texts. |
| **8\. ShathaTm/LK-Hadith-Corpus** | NLP Segmented Corpus | Unspecified / Academic | **Skip.** Primarily focused on basic segmentation and English translation, which is redundant for the target architecture. |
| **9\. CheeseWithSauce/HadithsJSONFormat** | Dataset | Unspecified | **Skip.** Redundant; provides basic hadith texts without advanced *isnad* graphs or critical evaluations. |
| **10\. Alsalka et al. / Qur'an Ontology** | Academic Research Paper | N/A | **Skip.** Focuses on semantic topic modeling and embeddings rather than structural *isnad* criticism. |

## **Detailed Evaluation of Top-Tier Candidates**

The following sections provide a granular architectural and epistemological breakdown of the top ten computational resources, analyzing how each addresses specific *takhreej* and *'ilal* operational requirements.

### **1\. ISNAD AI Governance Framework (alizahidraja/isnad)**

The ISNAD framework represents the most direct answer to the requirement for formalized, machine-readable *mustalah al-hadith* rules. It systematically adapts classical hadith transmission science into an operational Python-based architecture for multi-agent knowledge systems1.

* **Repository / Link:** https\://github.com/alizahidraja/isnad  
* **Owner / Authors:** alizahidraja  
* **Last Update Date:** Active (Backed by an academic preprint: arXiv:2607.24117).  
* **Stars / Citations / Adoption:** Highly rigorous, serving as a foundational text in AI provenance research that utilizes Islamic epistemological models1.  
* **Contents (Rules / Records):** The system implements a Directed Acyclic Graph (DAG) rule engine that computationally executes the *muta'akhkhirin* (later scholars, particularly Ibn Hajar) methodology1. It codifies:  
  * *Weakest-Link Rule:* The trust grade of an entire transmission chain is mathematically capped by its least reliable transmitter1.  
  * *Completeness (Ittiṣāl):* The engine programmatically downgrades any chain with a missing segment to weak (*munqati'*), overriding the individual reliability of the remaining narrators2.  
  * *Corroboration Upgrades (Mutaba'at):* Implements the epistemics of redundancy, wherein multiple independent weak chains mathematically elevate a claim's status, analogous to Condorcet's jury theorem3.  
  * *Decision Matrix Routing:* A core engine feature that weighs chain validity against *matn* (content) consistency. A perfect chain carrying contradictory content automatically triggers an *'Ilal* (hidden defect) flag for quarantine or human review2.  
* **License:** Code and Core Library are distributed under the Apache-2.0 license; the paper and documentation are under CC-BY-4.01. This is highly permissive and perfectly suited for commercial, open-source integration.  
* **Language and Format:** Python, JSON outputs, and DAG structural matrices1.  
* **Quality Evidence:** The rule engine is aggressively benchmarked. Its weakest-link algorithm successfully reproduced Ibn Hajar's 12 narrator tiers with a statistically significant Cohen’s ![][image1] across 575,060 graded hadith chains1. Furthermore, it achieved a 100% catch rate with zero false positives in adversarial narrator grading tests1.  
* **Known Problems:** The system relies on a philosophy of "operator responsibility for source boundary." Transmitters remain UNGRADED by default unless explicitly given priors by the operator. It does not auto-manufacture trust; it only computes the mathematical downstream effects of the trust parameters supplied to it1.  
* **Overlap and Integration (Easy):** Because the target application already utilizes Python and possesses a 23,500-narrator registry, integrating isnad is seamless. The application's existing heuristic "engineering guesses" for rules like *inqita'* and *hasan li-ghayrihi* can be completely deprecated and replaced by instantiating isnad’s \[langchain\] pipelines or its direct DAG evaluations.  
* **Verdict:** **Use directly.** This is the definitive computational rule engine for *mustalah al-hadith*.

### **2\. Itqan and the Ilm Platform (R3GENESI5/Itqan)**

This repository addresses the urgent need for structured data extracted directly from *ilal* and biographical dictionaries, providing the raw evaluative fuel required to power a rule engine like ISNAD.

* **Repository / Link:** https\://github.com/R3GENESI5/Itqan  
* **Owner / Authors:** R3GENESI5 (Ali Bin Shahid)  
* **Last Update Date:** Active (Continuous updates across versions 1.0–1.5).  
* **Stars / Citations / Adoption:** Highly visible; explicitly cited as the largest structured narrator database available in JSON format6.  
* **Contents (Rules / Records):** Itqan provides a staggering volume of structured JSON data. The dataset contains 112,221 Sunni hadiths across 18 books and a unified database of 115,735 narrator profiles6. Most critically for *'ilal* analysis, it houses over 152,000 distinct *jarh wa ta'dil* entries extracted directly from 22 classical encyclopedias, including *Tahdhib al-Kamal*, *Taqrib al-Tahdhib*, *Mizan al-I'tidal*, and *Al-Jarh wa al-Ta'dil*6. It encapsulates grades from authorities like Abu Hatim, Ahmad ibn Hanbal, and Ibn Hajar6. Furthermore, the repository's Ilm schema categorizes chain topology by mapping isnad\_analysis.breadth\_class (mutawatir, mashhur, aziz, gharib) and chain\_assessment.continuity7.  
* **License:** Code is MIT License. The classical texts and dictionaries are in the Public Domain, and NLP dependencies like CAMeL Tools are BSD-2-Clause6. This allows unrestricted commercial and open-source adaptation.  
* **Language and Format:** Flat JSON files (narrator\_unified.json, family\_corpus.json), HTML/JS, and SurrealDB (SurreQL) schema definitions6.  
* **Quality Evidence:** The repository effectively replaces decades of manual scholarly indexing. Its root-extraction methodology for mapping texts across the corpus achieved a remarkable 96.3% connectivity rate using a sophisticated dual-stemmer approach6. It contains 59,365 explicitly graded hadiths for validation6.  
* **Known Problems:** The project's documentation acknowledges that its rule engine is designed to report structural facts about chains based purely on classical dictionaries; it deliberately avoids issuing novel religious or jurisprudential rulings on hadith authenticity7.  
* **Overlap and Integration (Medium):** The target application currently utilizes a 23,500-narrator registry built from Shamela. Integrating Itqan requires executing the existing \~97% precision disambiguation algorithm to map internal IDs to Itqan’s 115,735 profiles and 217,762 name variants. Once mapped, the application inherits 152,000 expert evaluations instantly.  
* **Verdict:** **Use directly.** Itqan is the ultimate structural data layer for classical *rijal* criticism.

### **3\. KASHAF Visual Analysis and Matn Alignment (OmarShafie/hadith)**

Addressing the need for reusable components concerning *matn* (text) comparison and the algorithmic detection of *ziyadat al-thiqa* (reliable additions) and *shudhudh* (irregularities), KASHAF provides unparalleled mathematical logic.

* **Repository / Link:** https\://github.com/OmarShafie/hadith  
* **Owner / Authors:** OmarShafie  
* **Last Update Date:** Code last committed approximately 2–6 years ago8.  
* **Stars / Citations / Adoption:** Highly regarded as a specialized visual tool for *takhreej* and isnad mapping.  
* **Contents (Rules / Records):** KASHAF is not a static database, but a library of advanced algorithms. Its most critical feature is the Multiple Matn Alignment (MMA) module. It utilizes dynamic programming (specifically, Star Alignment Approximation and Edit Distance algorithms) to perform Multiple Sequence Alignment (MSA) across varying texts of the same hadith8. The algorithm synthesizes a composite text, replacing identical wording with dots (...), omitted words with ( x ), and leaving blank gaps to explicitly highlight *ziyadat* (additions) or *tafarrudat* (isolated reports)8. It also visualizes transmission flow using weighted Sankey diagrams, where node colors map to narrator grades from *Taqrib al-Tahdhib*8.  
* **License:** GPL-3.08. This is a viral copyleft license. If the code is directly embedded into a proprietary or differently-licensed open-source application, the entire application must be open-sourced under GPL-3.0.  
* **Language and Format:** Client-side JavaScript, HTML, Bootstrap, PEG.js for grammar parsing, and Google Charts8.  
* **Quality Evidence:** The mathematical alignment of Arabic text specifically optimized for identifying *shudhudh* is a notoriously complex Natural Language Processing (NLP) task. KASHAF handles this with high fidelity by computing the minimum edit operations required to transform one *matn* into another8.  
* **Known Problems:** The strict GPL-3.0 license poses integration challenges. Furthermore, the architecture is entirely browser-native and client-side; it lacks backend persistence or Python APIs8.  
* **Overlap and Integration (Hard):** Because the target application is Python-based, KASHAF cannot be cleanly dropped in. Integration requires either isolating KASHAF as an independent microservice or, preferably, translating its dynamic programming Edit Distance logic into Python using libraries like NumPy or SciPy to execute the sequence alignment matrix natively.  
* **Verdict:** **Use directly (with license caveats).** The mathematical logic for text alignment is indispensable for computing *'ilal*.

### **4\. Hadith Knowledge Graph (emadjumaah/hadith-kg)**

For researchers requiring datasets with expert labels to train or validate *takhreej* engines, this repository provides massive scale.

* **Repository / Link:** https\://huggingface.co/datasets/emadjumaah/hadith-kg  
* **Owner / Authors:** emadjumaah  
* **Last Update Date:** Active (Updated recently on Hugging Face)9.  
* **Stars / Citations / Adoption:** Recognized as the foundational ground-truth dataset utilized by the isnad-bench evaluation metrics10.  
* **Contents (Rules / Records):** A colossal structured dataset containing 715,790 hadiths and 577,024 transmission chains11. Its primary value lies in its 127,863 *jarh wa ta'dil* (criticism) statements generated by 1,015 distinct classical critics11. It includes a 54,862-node subject tree linked precisely to the hadith text level12.  
* **License:** CC-BY-4.01. Requires attribution but allows unrestricted commercial and open-source use.  
* **Language and Format:** Hugging Face Datasets format (CSV/Parquet distributions).  
* **Quality Evidence:** The dataset's efficacy is proven by its role as the empirical backbone for independent academic experiments like the ISNAD benchmark1.  
* **Known Problems:** Researchers note a specific quirk in the data schema: the field sanad\_rawis.pos \= 0 denotes the collection author, meaning the transmission direction ascends chronologically to the Companion. Utilizing this data for traditional top-down traversal requires recursive graph inversion12.  
* **Overlap and Integration (Easy):** This operates purely as a data layer. It complements the target application's existing 31 books by serving as a massive look-up table to validate narrator disambiguation and augment the *jarh* profiles.  
* **Verdict:** **Use directly.** Indispensable for historical *jarh wa ta'dil* labels.

### **5\. SemanticHadith Knowledge Graph V2 (A-Kamran/SemanticHadith-V2)**

For architects seeking a formal ontology—the structural backbone defining how to store hadith relationships—SemanticHadith represents the academic gold standard.

* **Repository / Link:** https\://github.com/A-Kamran/SemanticHadith-V2  
* **Owner / Authors:** A-Kamran (A. B. Kamran, B. Abro, A. Basharat)  
* **Last Update Date:** Version 2 published \~202313.  
* **Stars / Citations / Adoption:** Peer-reviewed and published in the *Journal of Web Semantics* (2023)13.  
* **Contents (Rules / Records):** This is a formal ontology defined in OWL (Ontology Web Language). It maps the exact semantic triples (Subject-Predicate-Object) required to represent a hadith, its narrators, transmission events, and structural topologies (e.g., has\_narrator, is\_student\_of)13.  
* **License:** CC0-1.0 (Creative Commons Zero / Public Domain)13.  
* **Language and Format:** OWL, RDF, and SPARQL queries executed via an OpenLink Virtuoso endpoint13.  
* **Quality Evidence:** Backed by rigorous academic publication and formalized ontology evaluation methodologies13.  
* **Known Problems:** An ontology is a schema, not an execution engine. It defines *what* a hadith graph looks like, but contains no mathematical logic for grading or resolving contradictions.  
* **Overlap and Integration (Medium):** If the target application intends to migrate its Python objects into a formal graph database (like Neo4j or SurrealDB), this ontology provides the exact schema design required to ensure international interoperability.  
* **Verdict:** **Use as reference.** Adopt its schema design to structure internal data elegantly.

### **6\. Dorar Hadith API (AhmedElTabarani/dorar-hadith-api)**

This tool addresses the need for validating computational outputs against contemporary expert consensus.

* **Repository / Link:** https\://github.com/AhmedElTabarani/dorar-hadith-api  
* **Owner / Authors:** AhmedElTabarani  
* **Last Update Date:** Active.  
* **Contents (Rules / Records):** A proxy API built to bypass CORS restrictions and query the authoritative *Dorar.net* hadith grading database15. It supports parameterized querying: developers can filter by grade (d\[\]=1 for Sahih, d\[\]=3 for Da'if) or by specific critics (m\[\]=256 for Al-Bukhari). It returns clean JSON mapping the text, narrator, critic, and final expert grade15.  
* **License:** Unspecified explicitly in snippets, generally assumed Open Source for the wrapper, but the underlying data belongs to Dorar.net15.  
* **Language and Format:** Node.js / Express proxy returning JSON15.  
* **Quality Evidence:** Dorar.net is universally recognized by modern researchers as a gold standard for digital classical grading aggregation.  
* **Known Problems:** This is a proxy scraper. Relying on it directly in a production application introduces massive dependency risks, including API schema changes, IP blocking, and potential terms of service violations15.  
* **Overlap and Integration (Easy):** Should be used strictly for CI/CD pipelines or local benchmarking. The proxy can dynamically feed thousands of hadiths into the target application to compute an accuracy benchmark against Dorar.net's human scholars.  
* **Verdict:** **Use as reference.** Skip for production; use exclusively for generating evaluation datasets.

### **7\. NAH-Hadith-Corpus (TaghreedT/NAH-Corpus)**

* **Repository / Link:** https\://github.com/TaghreedT/NAH-Corpus  
* **Contents:** An Arabic corpus containing 452,624 words compiled exclusively from Non-Authentic Hadiths (NAH) extracted from lesser-known texts, explicitly labeled with grades such as *Mawdu'* (fabricated) or *Da'if* (weak)16.  
* **Verdict:** **Use as reference.** A vital stress-testing asset. Any implemented *'ilal* rule engine must be evaluated against this corpus to ensure it correctly and consistently rejects fabricated texts and catches subtle defects.

### **8\. LK-Hadith-Corpus (ShathaTm/LK-Hadith-Corpus)**

* **Repository / Link:** https\://github.com/ShathaTm/LK-Hadith-Corpus  
* **Contents:** 39,038 annotated bilingual (English-Arabic) hadiths extracted from the six canonical books17.  
* **Verdict:** **Skip.** While the repository boasts a 92% accuracy in segmenting *isnad* from *matn*, the target application already utilizes a proprietary solution achieving \~97% precision17. The bilingual alignment is unnecessary for purely Arabic *mustalah* computations.

### **9\. HadithsJSONFormat (CheeseWithSauce/HadithsJSONFormat)**

* **Repository / Link:** https\://github.com/CheeseWithSauce/HadithsJSONFormat  
* **Contents:** Basic text extractions and categorizations scraped from Sunnah.com18.  
* **Verdict:** **Skip.** The target infrastructure already commands texts from 31 classical books. This repository offers no advanced *isnad* graph structures or critical evaluation data.

### **10\. Qur'an and Hadith Topic Ontology (Alsalka, Atwell, et al., 2024\)**

* **Contents:** Academic research (published in ICNLSP 2024\) utilizing word embeddings (AraBERT and Doc2Vec) to map semantic topics between Qur'anic verses and Hadith texts19.  
* **Verdict:** **Skip.** This research utilizes Deep Learning transformer models for contextual topic mapping. It does not address *isnad* routing, structural defect detection, or classical *jarh wa ta'dil* methodologies, rendering it irrelevant for rule-engine development19.

## **The Epistemological Divide: Algorithmic Gaps in Mustalah Formalization**

While the open-source community provides remarkable tools, a critical analysis against the user's five prioritized needs reveals profound epistemological constraints in the computational representation of Islamic sciences.

### **1\. The Gap in Pure Rule Extraction from Text**

**Requirement:** Structured data from *mustalah* books (e.g., *Nuzhat al-Nazar*, *Muqaddimat Ibn al-Salah*). **The Gap:** There is no repository that explicitly operates as a pure JSON or logic-gate transcription of *Nuzhat al-Nazar* itself. While systems like ISNAD encode its resulting theories (e.g., weakest-link) into code1, the classical books consist of discursive prose, historical anecdotes, and fluid dialectics21. The nuance in *Muqaddimat Ibn al-Salah* resists strict binary IF-THEN-ELSE digitization. Consequently, any tool attempting to automate *takhreej* relies on a reductionist interpretation of the original texts.

### **2\. The Algorithmic Conflict: Mutaqaddimin vs. Muta'akhkhirin Methodologies**

The target application aims to replace heuristic guesses regarding *ziyadat al-thiqa* (additions by reliable narrators), *shudhudh* (irregularities), and *'ilal* (hidden defects). However, the literature indicates that computational modeling forces an architectural choice between two historically divergent scholarly paradigms:

| Paradigm | Historical Proponents | Methodological Approach | Computational Representation |
| :---- | :---- | :---- | :---- |
| **Muta'akhkhirin (Late)** | Al-Nawawi, Ibn Hajar | **Rule-Based Formalism.** Heavily favors standardized rules. For example, Al-Nawawi ruled that *ziyadat al-thiqa* is universally admissible provided the narrator is *'adil* (just) and *dabit* (precise)22. | Highly compatible with DAG engines. The ISNAD tool perfectly encapsulates this late-era formalism by executing deterministic weakest-link grading1. |
| **Mutaqaddimin (Early)** | Al-Daraqutni, Ahmad ibn Hanbal | **Heuristic Tarjih (Preference).** Rejected blanket rules in favor of case-by-case analysis. Al-Daraqutni evaluated *ziyadat* by weighing the relative memory strength (*ahfaz*) of conflicting narrators; if a vastly superior narrator omitted a phrase, the addition by a lesser reliable narrator was flagged as a defect (*'illah*)22. | **Currently Non-Existent.** There are no open-source algorithms that effectively mimic this heuristic probability weighing. Current engines cannot probabilistically balance memory hierarchy against textual omissions. |

**The Unmet Need:** To achieve true early-scholar *'ilal* detection, an engine cannot rely solely on the graph continuity provided by tools like ISNAD. It must simultaneously deploy the dynamic sequence alignment found in KASHAF to identify textual anomalies, and then apply complex probabilistic weighting to determine if the anomaly represents an acceptable addition or a hidden defect. This represents the absolute frontier of computational hadith studies.

### **3\. Missing "Gold Standard" Multi-Book Graphs**

While hadith-kg and Itqan provide millions of individual data points6, there remains a gap for a unified "Gold Standard" JSON file that maps a single, highly debated hadith across all 31 books, complete with a verified, scholar-approved structural graph explicitly diagramming its *'ilal* resolution step-by-step. Developers must still manually assemble the data layers to reconstruct these complex historical arguments.

## **Strategic Integration Plan**

To successfully transition the existing 97%-accurate parsing engine into a rigorous, scholar-aligned *takhreej* and *'ilal* platform, the following phased architectural roadmap is recommended.

### **Phase 1: Data Architecture Overhaul (Weeks 1–2)**

> 1. **Clone R3GENESI5/Itqan**: Extract the narrator\_unified.json and family\_corpus.json files6.  
> 2. **Entity Resolution:** Execute the existing high-precision disambiguation algorithm to map the internal 23,500-narrator Shamela registry to Itqan’s 115,735 profiles.  
> 3. **Outcome:** The application instantly inherits 152,000 *jarh wa ta'dil* expert statements. Because Itqan is MIT-licensed, this data can be heavily mutated and stored natively without legal friction6.

### **Phase 2: Deprecating Heuristics for Formal Rule Engines (Weeks 3–4)**

> 1. **Integrate alizahidraja/isnad**: This is the most crucial software upgrade.  
> 2. **Implementation:** Deprecate the proprietary "engineering guesses" engine. Implement isnad's Python modules to construct Directed Acyclic Graphs (DAGs) for every *sanad* in the 31-book corpus1.  
> 3. **Execution:** Seed the isnad nodes with the *jarh wa ta'dil* priors obtained from Itqan. Run the isnad weakest-link and *mutaba'at* (corroboration) mathematical solvers. This guarantees the application's grading system adheres to formalized Ibn Hajar standards, immediately achieving an academic benchmark of ![][image1]1.

### **Phase 3: Algorithmic Matn Alignment for 'Ilal (Weeks 5–6)**

> 1. **Reference OmarShafie/hadith (KASHAF)**: Study the dynamic programming Edit Distance and Star Alignment Approximation algorithms located in js/MultipleMatnAlignment.js8.  
> 2. **Implementation (Clean Room):** Do not directly copy the GPL-3.0 JavaScript code to avoid viral licensing. Instead, write a bespoke Python equivalent utilizing SciPy or NLTK sequence alignment matrix functions.  
> 3. **Execution:** Feed the mathematically verified chains (from Phase 2\) into the sequence alignment algorithm. When identical *isnad* nodes diverge in the *matn*, flag the text algorithmically for *ziyadat al-thiqa* or *shudhudh*. This effectively re-creates the heuristic *'ilal* analysis of the *mutaqaddimin* by flagging textual anomalies within otherwise sound graphs.

### **Phase 4: Validation and Stress Testing (Week 7\)**

> 1. **Clone AhmedElTabarani/dorar-hadith-api** and download the **NAH-Hadith-Corpus**15.  
> 2. **Execution:** Generate an automated testing suite. Push 1,000 graded hadiths from the new custom engine against the Dorar.net proxy API to calculate the system's human-expert divergence rate. Subsequently, feed the fabricated hadiths from the NAH-Corpus into the engine to ensure the *'ilal* flags successfully trap malicious or deeply flawed transmissions.

The computational representation of *mustalah al-hadith* is no longer reliant on isolated, proprietary heuristics. By fusing the exhaustive *rijal* dataset from Itqan, the mathematical DAG evaluations of the ISNAD framework, and the sequence alignment logic of KASHAF, developers can construct a modular, open-source architecture that elegantly bridges classical Islamic epistemology with modern graph algorithms.

#### **Works cited**

> 1. ISNAD — Open-Source LLM Provenance & AI Audit Trail ... \- GitHub, [https\://github.com/alizahidraja/isnad](https://github.com/alizahidraja/isnad)  
> 2. Grading the Narrators: An Isnad-Rijal Framework for Claim-Level, [https\://arxiv.org/pdf/2607.24117](https://arxiv.org/pdf/2607.24117)  
> 3. Grading the Narrators: An Isnād–Rijāl Framework for Claim-Level, [https\://arxiv.org/html/2607.24117v1](https://arxiv.org/html/2607.24117v1)  
> 4. isnad/js/README.md at main · alizahidraja/isnad · GitHub, [https\://github.com/alizahidraja/isnad/blob/main/js/README.md](https://github.com/alizahidraja/isnad/blob/main/js/README.md)  
> 5. isnad/LICENSE at main · alizahidraja/isnad · GitHub, [https\://github.com/alizahidraja/isnad/blob/main/LICENSE](https://github.com/alizahidraja/isnad/blob/main/LICENSE)  
> 6. Computational Quran–Hadith Concordance & Rijal Database \- GitHub, [https\://github.com/R3GENESI5/Itqan](https://github.com/R3GENESI5/Itqan)  
> 7. Searching the Quran and Sunnah with Graph, Vector, Full-Text, [https\://arriqaaq.com/ilm/articles/launch.html](https://arriqaaq.com/ilm/articles/launch.html)  
> 8. KASHAF: The Hadith Visual Search Engine \- GitHub, [https\://github.com/OmarShafie/hadith](https://github.com/OmarShafie/hadith)  
> 9. Datasets – Hugging Face, [https\://huggingface.co/datasets?other=arabic\&p=14\&sort=trending](https://huggingface.co/datasets?other=arabic&p=14&sort=trending)  
> 10. alizahidraja/isnad-bench · Datasets at Hugging Face, [https\://huggingface.co/datasets/alizahidraja/isnad-bench](https://huggingface.co/datasets/alizahidraja/isnad-bench)  
> 11. isnad/bench/README.md at main · alizahidraja/isnad · GitHub, [https\://github.com/alizahidraja/isnad/blob/main/bench/README.md](https://github.com/alizahidraja/isnad/blob/main/bench/README.md)  
> 12. emadjumaah/hadith-kg · Datasets at Hugging Face, [https\://huggingface.co/datasets/emadjumaah/hadith-kg](https://huggingface.co/datasets/emadjumaah/hadith-kg)  
> 13. A-Kamran/SemanticHadith-V2 \- GitHub, [https\://github.com/A-Kamran/SemanticHadith-V2](https://github.com/A-Kamran/SemanticHadith-V2)  
> 14. oasis-tcs/tac-ontology: OASIS-TAC-TC: OWL representation ... \- GitHub, [https\://github.com/oasis-tcs/tac-ontology](https://github.com/oasis-tcs/tac-ontology)  
> 15. AhmedElTabarani/dorar-hadith-api: API وسيط للـدرر السنية \- GitHub, [https\://github.com/AhmedElTabarani/dorar-hadith-api](https://github.com/AhmedElTabarani/dorar-hadith-api)  
> 16. GitHub \- TaghreedT/NAH-Corpus: Non-authentic Hadith Corpus, [https\://github.com/TaghreedT/NAH-Corpus](https://github.com/TaghreedT/NAH-Corpus)  
> 17. ShathaTm/LK-Hadith-Corpus: Leeds University and King ... \- GitHub, [https\://github.com/ShathaTm/LK-Hadith-Corpus](https://github.com/ShathaTm/LK-Hadith-Corpus)  
> 18. GitHub \- CheeseWithSauce/HadithsJSONFormat: Free, authentic, [https\://github.com/CheeseWithSauce/HadithsJSONFormat](https://github.com/CheeseWithSauce/HadithsJSONFormat)  
> 19. Linking Quran and Hadith Topics in an Ontology using Word, [https\://aclanthology.org/2024.icnlsp-1.46.pdf](https://aclanthology.org/2024.icnlsp-1.46.pdf)  
> 20. Linking Quran and Hadith Topics in an Ontology using Word, [https\://eprints.whiterose.ac.uk/id/eprint/219409/1/2024.icnlsp-1.46.pdf](https://eprints.whiterose.ac.uk/id/eprint/219409/1/2024.icnlsp-1.46.pdf)  
> 21. Rahat-ul-Quloob / Vol.9, Issue.1 (Jan-Jun: 2025), [https\://rahatulquloob.com/index.php/rahat/article/download/476/482/](https://rahatulquloob.com/index.php/rahat/article/download/476/482/)  
> 22. (PDF) Analysis On Al-Nawawi's Criticism On Al-Daraqutni's, [https\://www\.researchgate.net/publication/346190331\_Analysis\_On\_Al-Nawawi's\_Criticism\_On\_Al-Daraqutni's\_Methodology\_In\_'Ilal\_Al-Hadith](https://www.researchgate.net/publication/346190331_Analysis_On_Al-Nawawi's_Criticism_On_Al-Daraqutni's_Methodology_In_'Ilal_Al-Hadith)

[image1]: <data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAEwAAAAaCAYAAAAdQLrBAAAD6klEQVR4Xs2YO2gVQRSGz+IDRVEDwQeoXMUmiKgECy0sxEaIIij4KrSLhTYKCmKRRkTsbBQLxUJsbCyMjUjQTgsbRREEDVaRFAqKD3ycf8/svfOe2ftKPvizO+ecOTt7dnZmb4h6QhE4D1k8ZAXVoAv5tBTpbOkIRSAwYI5Qp0ed2ADeFC2j4fbGerEj7XZv6fhqiQQhd8hORkUjUbOC3AFmhQWCAmYh6Aw6HPIjyQmO9J3HWstaZjsckKSVaA5rFWu7OtrsZB3jeOnhHYA2gTRrbWKdYz6XKtrbC8ZDrGk+meDjO9ZTPm/oQRHekvS7yXrJGiHzQhdY/1i/WJPsmpRjU5u02Dbx3JfH1CTsC3s0DrB+s07ILCiW8vlj1hvWaiPSZStrh9bGBb+x9mm2uyQFa6owj7ieImu8PSTv+n9Z91lzNdsQd53m4wvNZrOYMLOK8lXWuUMoeEGLylZBD/nvoBEhDwKzEQ8rCZ7iAP/Fu287knhjvEYK2zXUU8ZN6mAt+sjer5ZdUkpeFBiFRt8lLSc9Z50vz4RTrAVaGwW+wTpHyRGK+yTJU92jrAtZp1kflBSJXBZmtLQyM0QKVo7TQcuOVw/9v7COsAZYj8h4zSyK8v4fkNy3i2fQmKLfWdtUG+vFLZLB/ayCAgxyvoNkq/DYWkoRKFiBgsEXA7f3h8w1yl+IFu9JdtRsUJgnJNPyouWbCQIFK2dYqmBbWGMkrxxmGGYa+lwnc02sGCbZNTOR6YaE2JVwnGLTUUo8ldwXLB3hJa9gbvLlrFeFrEkVWMzRB2+KzCKjX3GJ/+zVLSmws2DbRaWRqkFygXFuwtdf5Cs+VjCMNcRhcpaQMt9lkpyjpq9c37DrVkuRRVVZ88kMkXRC5wq0sRttVu31ms9mN5nrRY6iFBKDdVXfyTAWjOm1ZrPBTvjJNnJCTAYU2i5YlXPYnayWRWteI/xMMMGgJkhmH764fe++hnu5DjlLsnDj2kiPV+w2Hz/zcaOKqW4WxZ2vbNUH7gbVrsD6jK9++/vsDEl/FDQLfMjhAussOwaMnw3jXIpnlq9jUuUt5MaukOzcWGOw5U+xdjUj5GHeY/0QW5OVJGOHD/eBr/qr5F+TRzkVCub7vRkktE7BvoLcp9ImqTJ5WUPyGYIfy+Y4nHSGocHazzpO8QUdRRyh6ke4wkndF3BV+8p2uw3cFMriOnqJdXflaX9HkEWbQ4p2izozkRzdyNQHag2zVnCc7FTdeAM66twtrIWrtARasf/wRlxt00nOTvrOCqI3EHUm3S2SgcmAPpE9juzAMF1I0aROLifWMQilOeALUTN8hqhGaR+7g57tP7AloDwqVRJdAAAAAElFTkSuQmCC>