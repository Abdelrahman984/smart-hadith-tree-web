import fs from 'fs';

const data = JSON.parse(fs.readFileSync('data/ara-bukhari.json', 'utf8'));

function removeTashkeel(text) {
  return text
    .replace(/[\u0617-\u061A\u064B-\u0652]/g, '')
    .replace(/[\u0640]/g, '')
    .replace(/\uFEFF/g, '');
}

function extractIsnadChain(rawText) {
  const clean = removeTashkeel(rawText);

  // Stop markers where Isnad ends and Matn begins
  const matnStopMarkers = [
    'قال رسول الله',
    'سمعت رسول الله',
    'ان رسول الله',
    'أن رسول الله',
    'عن النبي صلى الله عليه وسلم قال',
    'عن النبي صلى الله عليه وسلم قال',
    'عن النبي صلى الله عليه وسلم',
    'قال النبي صلى الله عليه وسلم',
    'يقول : سمعت رسول الله',
    'سأل رسول الله',
    'أنها قالت أول ما بدئ',
    'يقول : " إنما',
    'يقول : "انما',
    'قال : " بني',
    'قال : " المسلم',
    'قال : " الإيمان',
  ];

  let matnIndex = -1;
  for (const marker of matnStopMarkers) {
    const idx = clean.indexOf(marker);
    if (idx !== -1 && (matnIndex === -1 || idx < matnIndex)) {
      matnIndex = idx;
    }
  }

  // If no explicit marker, try to look for typical quote or punctuation or 'قال' near middle
  const isnadPart = matnIndex !== -1 ? clean.slice(0, matnIndex) : clean.slice(0, Math.min(300, clean.length));

  // Regex to match transmission verbs:
  // حدثنا, حدثني, أخبرنا, أخبرني, أنبأنا, عن, سمعت, قال
  const termRegex = /(حدثنا|حدثني|أخبرنا|أخبرني|أنبأنا|عن|أنه سمع|سمعت|سمع|أخبره أن|أخبره)\s+/g;
  
  const matches = [...isnadPart.matchAll(termRegex)];
  if (matches.length === 0) return [];

  const chain = [];
  for (let i = 0; i < matches.length; i++) {
    const term = matches[i][1];
    const startIndex = matches[i].index + matches[i][0].length;
    const endIndex = (i + 1 < matches.length) ? matches[i + 1].index : isnadPart.length;

    let narratorChunk = isnadPart.slice(startIndex, endIndex);

    // Clean narratorChunk: remove 'قال', 'أنه', honorifics, punctuation
    narratorChunk = narratorChunk
      .replace(/،\s*قال\s*[:\s]*/g, ' ')
      .replace(/قال\s*[:\s]*/g, ' ')
      .replace(/أنه\s+سمع\s+/g, ' ')
      .replace(/رضي الله عنهما|رضى الله عنهما|رضي الله عنها|رضى الله عنها|رضي الله عنه|رضى الله عنه/g, '')
      .replace(/رحمه الله/g, '')
      .replace(/[،,:."”«»\[\]\(\)\{\}\-]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    // Cut off if it has 'يقول' or 'أنه' at the end
    narratorChunk = narratorChunk.replace(/\s+(يقول|أنه|أنها)$/, '').trim();

    if (narratorChunk.length >= 2 && narratorChunk.length <= 60 && !narratorChunk.includes('رسول الله')) {
      chain.push({ term, narrator: narratorChunk });
    }
  }

  return chain;
}

console.log('Testing on first 15 hadiths:');
for (let i = 0; i < 15; i++) {
  const h = data.hadiths[i];
  const chain = extractIsnadChain(h.text);
  console.log(`\n[Hadith #${h.hadithnumber}] (${chain.length} narrators)`);
  chain.forEach((step, idx) => {
    console.log(`  ${idx + 1}. [${step.term}] ${step.narrator}`);
  });
}
