import { PageHeader } from '../../components/PageHeader';

// Funnel (recommended -> approved -> hired) + risk counts, backed by
// IReportingRepository.GetExecutiveDashboardAsync on the API.
export function ExecutiveDashboard() {
  return <PageHeader title="Executive Dashboard" />;
}
