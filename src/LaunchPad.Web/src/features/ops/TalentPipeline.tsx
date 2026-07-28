import { Title2 } from '@fluentui/react-components';

// Sponsor/Ops/Exec/HiringManager talent pipeline view. CandidateDto already omits
// hidden scores server-side for unauthorized roles — no client-side filtering needed.
export function TalentPipeline() {
  return <Title2>Talent Pipeline</Title2>;
}
