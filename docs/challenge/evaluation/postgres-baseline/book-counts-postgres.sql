-- Same columns as book-counts-sqlserver.csv (phase 0). DATALENGTH/2 there counts UTF-16 units, char_length here
-- counts code points; they agree unless a text has characters outside the BMP.
SELECT h."NormalizedBookName" AS "Book", COUNT(*) AS "Hadiths",
  SUM(char_length(h."MatnArabic")) AS "MatnChars",
  SUM(COALESCE(char_length(h."FullIsnadText"), 0)) AS "IsnadChars",
  (SELECT COUNT(*) FROM "Transmissions" t JOIN "Hadiths" h2 ON h2."Id" = t."HadithId" WHERE h2."NormalizedBookName" = h."NormalizedBookName") AS "Transmissions",
  (SELECT SUM(t."StepOrder") FROM "Transmissions" t JOIN "Hadiths" h2 ON h2."Id" = t."HadithId" WHERE h2."NormalizedBookName" = h."NormalizedBookName") AS "StepOrderSum"
FROM "Hadiths" h GROUP BY h."NormalizedBookName" ORDER BY h."NormalizedBookName";
