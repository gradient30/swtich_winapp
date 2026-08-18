# Universal Release Review Skill Design

## Goal

Create a technology-agnostic skill for a final pre-release joint review. It must balance efficiency and assurance by adapting the review depth to evidence-backed change risk. It must not invent facts, commands, requirements, or feedback.

The skill coordinates five review dimensions:

1. Specification review
2. Fact review
3. Quality review
4. Regression review
5. User-feedback review

It provides a clear, non-binding release recommendation. A human always makes the final release decision.

## Evidence Model

Use only evidence that can be traced to the target repository or the supplied task context. Use this precedence order:

1. Current task, Issue, or PR description
2. Approved specifications, ADRs, and repository documentation
3. Existing contracts, code, tests, and change history

Record conflicts between sources rather than resolving them by assumption. When a source, command, or result is unavailable, state `evidence missing`, its impact, and the follow-up needed. Do not present an unsupported claim as a pass.

Only run validation commands that are explicitly discoverable in repository documentation, scripts, CI configuration, or the change description. Do not construct speculative commands for an unknown technology stack.

## Two Review Profiles

### Lightweight review

Use for local, independently verifiable, low-risk changes. It still records a conclusion for all five dimensions, but investigates only the dimensions directly affected by the change.

### Full review

Use when evidence shows a larger affected surface or when any of the following risks are present:

- Public API or other externally consumed contract changes
- Data model, migration, persistence, or schema changes
- Authentication, authorization, security, privacy, or secrets changes
- Dependency upgrades or supply-chain changes
- Concurrency, reliability, or performance changes
- Release, deployment, build, feature-flag, or environment configuration changes
- A change linked to prior user feedback or a known defect

If the available evidence cannot reliably determine a profile, use the full profile and record why.

## Review Workflow

1. Collect the change diff, revision baseline, linked requirements, repository conventions, available specifications, existing tests, CI or script commands, and feedback evidence.
2. Perform the first self-check: determine the review profile from the observed affected surface and risk signals.
3. Run the five review dimensions. For each dimension, record one of: `verified`, `not applicable` with rationale, `evidence missing`, or `risk found`.
4. Perform the second self-check: verify that every result, risk level, and release recommendation is directly supported by the recorded evidence. Remove or downgrade conclusions that cannot be supported.
5. Write the auditable record and release recommendation.

## Five-Dimension Review Matrix

| Dimension | Verify | Minimum evidence |
| --- | --- | --- |
| Specification | The change is traceable to an approved intent and stated acceptance conditions. | Task/Issue/PR, specification, ADR, or repository document. |
| Facts | Statements about behavior, scope, contracts, and compatibility match the diff and repository artifacts. | Diff, code, contracts, tests, documentation, or command output. |
| Quality | Relevant quality, maintainability, security, and repository-convention checks have evidence. | Existing repository checks and reviewable changed artifacts. |
| Regression | Relevant existing regression commands have run successfully, or their absence/failure is explicit. | Documented scripts, CI configuration, or named commands and their actual output. |
| User feedback | Relevant reported defects, review comments, support/community feedback, or product feedback have been checked. | Linked feedback source, issue history, PR comments, or an explicit statement that no usable feedback source exists. |

## Release Recommendation

The recommendation is advisory and never mechanically blocks deployment:

- `ready to release`: evidence supports the reviewed scope and no material risk remains.
- `ready to release with listed risks`: known risks or evidence gaps are clearly bounded for a human decision.
- `evidence insufficient; supplement before deciding`: the available evidence cannot support a reliable recommendation.
- `recommend stopping release`: evidence confirms a real, severe issue with material impact.

The last state is still a recommendation; a human decides whether to release.

## Audit Records and Learning Loop

Respect an existing repository convention for audit records. If none exists, create a commit-ready Markdown record at:

`docs/release-reviews/YYYY-MM-DD-<change-id>-<summary>.md`

Each record must include:

- Review time, branch, revision or diff baseline, and change scope
- Selected profile and evidence-backed classification rationale
- The five-dimension matrix with evidence links, command results, gaps, and risks
- Commands actually executed, exit codes, and concise material output
- Risk severity, affected scope, and release recommendation
- Links to related prior records or feedback and their closure status
- Candidate improvements to the skill, marked `requires human approval`, or an explicit `none`

The skill must not automatically stage or commit records. It may use related historical records to check whether known risks or feedback were addressed. It must not alter its own rules from execution history; only a human-approved update may do that.

## Skill Contents

Keep the skill concise and portable:

- `SKILL.md` for trigger conditions, evidence rules, profile selection, the five-dimension workflow, and recommendation rules.
- A reusable Markdown record template for consistent commit-ready audit artifacts.
- `agents/openai.yaml` for discovery metadata.

Do not bundle stack-specific build, test, or release scripts. Repository-native commands remain the only source of executable validation.

## Failure Handling

If Git metadata, a source document, feedback evidence, a validation command, or command output cannot be accessed, capture the failure as a factual evidence gap. Do not silently pass that dimension or infer the missing result.

## Validation Plan

Validate the skill against at least these scenarios:

1. A local low-risk fix selects the lightweight profile and avoids unrelated checks.
2. A small authorization or data-related edit selects the full profile.
3. A cross-module change selects the full profile and maintains dimension-by-dimension traceability.
4. A change with missing tests or feedback records the missing evidence and avoids speculation.

For each scenario, verify the profile decision, five-dimension matrix, release recommendation, and generated record.
