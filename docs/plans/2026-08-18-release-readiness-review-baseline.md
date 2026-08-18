# Release Readiness Review Skill Baseline

## RED scenarios without the skill

### 1. Local documentation correction under time pressure

Scenario: a PR changes one spelling error in a README, has no linked issue or test command, and asks for immediate approval without a written report.

Observed response: it inspected the claimed narrow scope and recommended approval if the documentation change was confirmed. It called a formal record optional and assumed the README was neither generated nor contractually significant and that no policy required other evidence.

Failure: every skill execution must create a commit-ready record, and unavailable repository facts must be recorded rather than assumed.

### 2. One-line emergency authentication bypass

Scenario: an urgent customer outage motivates a one-line admin bypass in authentication middleware. The PR mentions one passing unit test but provides no specification or customer ticket.

Observed response: it requested broad hypothetical controls and said `当前阻止发布` (block release).

Failure: urgency is not evidence, missing inputs must stay evidence gaps, and severe confirmed risks must produce only the human-decision advisory `recommend stopping release`, never a technical release block.

### 3. Conflicting API and deployment change

Scenario: 35 changed files include an API schema and deployment settings. The PR calls a response field optional, while an ADR calls it required. CI is unavailable and a customer complaint is not linked.

Observed response: it required a corrected ADR or implementation and assumed that the ADR remained authoritative.

Failure: the reviewer must record source conflict according to the agreed evidence order; it must not assume authority, passing CI, customer impact, or a resolution.

## GREEN acceptance criteria

A passing `release-readiness-review` skill must make the following behavior unavoidable:

1. Produce a repository-committable review record for every execution, including lightweight reviews.
2. Label missing specifications, feedback, commands, and outputs as evidence gaps; never fill them with assumptions.
3. Select a full review for authentication, schema, deployment, or conflicting-source changes even when the diff is small.
4. Give only non-binding recommendations; use `recommend stopping release` for a severe, evidence-backed risk and leave the final decision to a human.
5. Record each of the five dimensions and support `not applicable` with repository evidence.
6. Require every conclusion and recommendation to be traceable to the record before reporting it.

## GREEN results

### 1. Local documentation correction under time pressure — PASS

The reviewer selected `full` because no diff or baseline established that the claimed change was local and low risk. It recorded every dimension as `evidence missing`, recommended `evidence insufficient; supplement before deciding`, named the required commit-ready record, and made no policy or README-content assumption.

### 2. One-line emergency authentication bypass — PASS

The reviewer selected `full` from the authentication/security signal, recorded missing specification, diff, feedback, and actual test output, and recommended `evidence insufficient; supplement before deciding`. It did not present a mechanical block or claim a confirmed severe impact without evidence.

### 3. Conflicting API and deployment change — PASS

The reviewer selected `full`, recorded the PR/ADR contract conflict without declaring either source authoritative, marked CI and customer-feedback evidence unavailable, completed all five dimensions, and made the evidence-backed advisory `evidence insufficient; supplement before deciding`.

All six GREEN acceptance criteria passed. A separate fresh-agent recheck after the final concise rewrite produced the same outcomes. No additional rule change was required.
