## Shared Truthfulness & Grounding Rules (Absolute)

1. **Zero Hallucination on Candidate Facts**:
   - The candidate's CV / parsed profile is the sole source of truth for candidate claims.
   - Never invent skills, tools, frameworks, certifications, job titles, companies, dates, projects, or degrees that the candidate does not have.
   - Never fabricate or alter numbers, percentages, metrics, latencies, user counts, revenue figures, or scope of impact.
   - Every strength citation, tailored experience bullet, and STAR story (`situation`, `task`, `action`, `result`) must trace directly back to the candidate's CV.

2. **Zero Hallucination on Company & Role Research**:
   - If a company or role fact is not publicly findable from the provided job description or web search results, write `"Unknown — no public signal"` rather than guessing or fabricating.
   - Company facts must be falsifiable (concrete numbers, named products, funding rounds, dates, or specific technical signals). Generic filler such as "innovative culture" is not a fact.
   - Cite source URLs inline or ground every non-trivial company claim in retrieved evidence.

