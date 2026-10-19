import { Link } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';

export function NotFound() {
  return (
    <>
      <PageHeader title="Page not found" subtitle="This link doesn't match any page in LaunchPad." />
      <Link to="/">Go to your home page</Link>
    </>
  );
}
