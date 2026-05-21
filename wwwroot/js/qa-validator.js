// Q&A Stage 2 POS-dependent quality rules.
// Loaded on demand by Services/QnA/QualityValidator.cs via dynamic import().
//
// Implements two rules that the C# QualityRules library cannot express with
// regex alone:
//
//   - first-person-subject: every sentence must use "I" as its grammatical
//     subject, or open with an allowed sentence-initial preposition /
//     demonstrative. We don't have a dependency parser in the browser, so the
//     check is approximate: the first content token of the sentence is "I" or
//     in the allow-list (As/During/On/In/While/This/That), OR the sentence
//     contains the standalone pronoun "I" somewhere. Sentences with no "I"
//     at all and a non-allow-list opener are flagged.
//
//   - source-fidelity-named: every proper noun (POS = PROPN per wink-nlp's
//     lite-web model) and every bare numeric token in the answer must appear
//     somewhere in the concatenated resumeEvidence of the selected priorities.
//     Conservative scope per the design — paraphrased capability drift is
//     intentionally not flagged here.
//
// wink-nlp loads from jsDelivr's +esm transform. First load fetches the model
// (~1 MB compressed). Q&A already requires network for the AI call, so this
// is not a meaningful regression to PWA offline behavior.

import winkNlp from "https://cdn.jsdelivr.net/npm/wink-nlp@2.4.0/+esm";
import modelModule from "https://cdn.jsdelivr.net/npm/wink-eng-lite-web-model@1.8.1/+esm";

// Some bundlers wrap the default export; tolerate both shapes.
const model = modelModule?.default ?? modelModule;
const nlp = winkNlp(model);
const its = nlp.its;

// Words allowed as a sentence's first token without further checks. Covers
// the alternative openers listed in the Stage 2 prompt example list
// ("As [role/owner] of [system]…", "During [period]…", "On [project]…",
// "In [domain]…"), plus demonstratives ("This"/"That" referring to the
// prior sentence) and the gap-template opener ("While I have not…").
// "i" is the canonical first-person subject.
const ALLOWED_FIRST = new Set([
    "i", "as", "during", "on", "in", "while", "this", "that",
]);

// Common tokens to skip when running the source-fidelity check. The lite-web
// POS model occasionally mistags sentence-initial function words as PROPN
// because of the capital letter; this list prevents those mistags from
// producing spurious violations. Real proper nouns (Azure, Acme, ICD-10) are
// nowhere near this list.
const FIDELITY_STOPWORDS = new Set([
    "I", "A", "An", "The", "This", "That", "These", "Those",
    "As", "During", "On", "In", "While", "With", "For",
    "My", "Our", "Their", "Whose",
    "Yes", "No",
]);

// Bare numeric tokens: counts, percentages, dotted/comma decimals. Years are
// included intentionally — if an answer claims "12 years" but evidence says
// nothing about 12, that's a fabrication.
const NUMERIC = /^\d+(?:[.,]\d+)?%?$/;

const SENTINEL_PREFIX = "I cannot determine that from the provided resume and role information";

// Sentence segmentation. wink-nlp's lite-web model treats any "." as a
// potential sentence terminator and produces phantom sentences mid-token for
// names like ".NET", "Node.js", "U.S." This regex mirrors the C# QualityRules
// splitter: only split on a sentence-terminating punctuation character that
// is followed by whitespace. "workflows." + space + "In" splits; "full-stack
// .NET" does not.
const SENTENCE_SPLIT = /(?<=[.!?])\s+/g;

function splitSentences(text) {
    return text
        .split(SENTENCE_SPLIT)
        .map(s => s.trim())
        .filter(s => s.length > 0);
}

function truncate(s, n) {
    return s.length <= n ? s : s.slice(0, n) + "…";
}

function firstAlphabeticWord(sentence) {
    const m = sentence.match(/[A-Za-z][A-Za-z'-]*/);
    return m ? m[0].toLowerCase() : null;
}

function checkFirstPersonSubject(answerText, hits) {
    for (const text of splitSentences(answerText)) {
        const firstWord = firstAlphabeticWord(text);
        if (firstWord === null) continue;

        if (ALLOWED_FIRST.has(firstWord)) continue;

        // Permissive fallback: a sentence containing the standalone pronoun "I"
        // is almost certainly first-person regardless of the literal opener.
        // Only flag if "I" never appears in the sentence.
        if (/\bI\b/.test(text)) continue;

        hits.push({
            ruleId: "first-person-subject",
            description: "Sentence does not use 'I' as its grammatical subject and does not open with an allowed preposition or demonstrative.",
            matchedText: truncate(text, 80),
            context: truncate(text, 120),
        });
    }
}

function checkSourceFidelityNamed(doc, evidenceText, hits) {
    const haystack = evidenceText.toLowerCase();
    const seen = new Set();

    doc.tokens().each((t) => {
        const word = t.out();
        if (FIDELITY_STOPWORDS.has(word)) return;
        if (word.length < 2) return;

        const pos = t.out(its.pos);
        const isNumeric = NUMERIC.test(word);
        const isProper  = pos === "PROPN";
        if (!isNumeric && !isProper) return;

        const key = word.toLowerCase();
        if (seen.has(key)) return;
        seen.add(key);

        if (haystack.includes(key)) return;

        hits.push({
            ruleId: "source-fidelity-named",
            description: "Proper noun or numeric metric in answer is not present in any selected resumeEvidence.",
            matchedText: word,
            context: `"${word}" not found in evidence`,
        });
    });
}

// Invoked by the C# QualityValidator with the raw answer text and the list of
// resumeEvidence strings from the SELECTED ROLE FIT PRIORITIES. Returns an
// array of hit records the C# side maps to QualityViolation. Returns an
// empty array on the insufficient-data sentinel (no Stage 2 output to grade).
export function evaluate(answerText, evidenceList) {
    if (!answerText || answerText.trim().length === 0) return [];
    if (answerText.trimStart().startsWith(SENTINEL_PREFIX)) return [];

    const hits = [];

    // Sentence-level rule uses string segmentation only — no wink dependency,
    // and immune to mid-token "." mis-splits.
    checkFirstPersonSubject(answerText, hits);

    // Token-level rule uses wink for POS tagging. doc.tokens() walks the whole
    // document so sentence-segmentation fidelity is irrelevant here.
    const evidenceText = (evidenceList || []).join(" · ");
    if (evidenceText.length > 0) {
        const doc = nlp.readDoc(answerText);
        checkSourceFidelityNamed(doc, evidenceText, hits);
    }

    return hits;
}
