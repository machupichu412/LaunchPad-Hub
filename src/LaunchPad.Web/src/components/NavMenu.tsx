import { Link } from 'react-router-dom';
import { makeStyles, tokens } from '@fluentui/react-components';
import { AppRoles } from '../auth/roles';
import { useActiveRole } from '../auth/ActiveRoleContext';

const useStyles = makeStyles({
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    marginTop: tokens.spacingVerticalM,
    listStyle: 'none',
    padding: 0,
  },
});

/**
 * Shapes the menu only — every link it exposes still hits an API endpoint that
 * independently re-checks authorization. See RequireRole / CLAUDE.md.
 *
 * Filters by the header's active role (single "viewing as" perspective), not
 * every role the user holds — a Sponsor+Candidate seeing both role's nav items
 * mixed together at once wouldn't match the mockup's per-role experience.
 */
export function NavMenu() {
  const styles = useStyles();
  const { activeRole } = useActiveRole();

  return (
    <ul className={styles.list}>
      <li><Link to="/">Home</Link></li>

      {activeRole === AppRoles.Candidate && (
        <>
          <li><Link to="/dashboard">Dashboard</Link></li>
          <li><Link to="/profile">My Profile</Link></li>
          <li><Link to="/assignments">Assignments</Link></li>
          <li><Link to="/tasks">Tasks</Link></li>
          <li><Link to="/deliverables">Deliverables</Link></li>
          <li><Link to="/evaluations">Evaluations</Link></li>
          <li><Link to="/community">Community</Link></li>
        </>
      )}

      {activeRole === AppRoles.Sponsor && (
        <>
          <li><Link to="/projects">My Projects</Link></li>
          <li><Link to="/pipeline">Talent Pipeline</Link></li>
        </>
      )}

      {activeRole === AppRoles.ProgramOps && (
        <>
          <li><Link to="/ops/dashboard">Dashboard</Link></li>
          <li><Link to="/ops/projects">Projects</Link></li>
          <li><Link to="/ops/approvals">Approvals</Link></li>
          <li><Link to="/ops/cohorts">Cohorts</Link></li>
          <li><Link to="/ops/risks">Risks</Link></li>
          <li><Link to="/pipeline">Candidates</Link></li>
          <li><Link to="/community">Community</Link></li>
        </>
      )}

      {activeRole === AppRoles.Executive && (
        <>
          <li><Link to="/pipeline">Talent Pipeline</Link></li>
          <li><Link to="/exec">Executive Dashboard</Link></li>
        </>
      )}

      {activeRole === AppRoles.HiringManager && (
        <li><Link to="/pipeline">Talent Pipeline</Link></li>
      )}
    </ul>
  );
}
