import { PageHeader } from '../../components/PageHeader';

export function Unauthorized() {
  return (
    <PageHeader
      title="Not authorized"
      subtitle="Your account doesn't have access to this page. Contact Program Ops if this seems wrong."
    />
  );
}
