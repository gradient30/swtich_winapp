# Release Readiness Review Chinese Output Design

## Goal

Make the release-review record Chinese by default without weakening evidence fidelity or altering untranslatable technical information.

## Chosen Approach

Use one Chinese canonical record rather than parallel Chinese and English documents.

- Translate headings, field labels, review conclusions, risk descriptions, recommendations, non-code explanations, and log interpretations into Chinese.
- Preserve code, identifiers, file paths, commands, exit codes, API names, error codes, and other terms that cannot be reliably translated.
- Preserve raw command output verbatim as factual evidence. Add a separate Chinese interpretation of that output; do not replace raw output with a translation.
- Do not create a duplicate English document. The Chinese record is the commit-ready source of review truth.

## Record Template Changes

The template will have Chinese section names and table headings. The executed-command section will distinguish:

- Original command and raw output
- Exit code
- Chinese result interpretation

The evidence, five-dimension matrix, evidence gaps, risk register, release recommendation, history, and human-approval sections will use Chinese labels and status wording. Inline technical terms remain unchanged where translation would be lossy.

## Skill Workflow Changes

The workflow will require Chinese output by default and direct the reviewer to keep raw technical evidence intact. It will prohibit translating a raw log or command result in a way that changes its factual meaning.

## Validation

Validate that the template contains Chinese review fields, the raw-output field, and Chinese interpretation field; validate the skill structure; then forward-test a scenario that contains an English command result to ensure it retains the original output and provides a Chinese explanation.
