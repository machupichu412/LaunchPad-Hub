import { Link } from 'react-router-dom';
import { makeStyles, tokens } from '@fluentui/react-components';
import { AppRoles, type AppRole } from '../auth/roles';
import { useRoles } from '../auth/useRoles';

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
 */
export function NavMenu() {
  const styles = useStyles();
  const roles = useRoles();

  const pipelineRoles: AppRole[] = [AppRoles.Executive, AppRoles.ProgramOps, AppRoles.Sponsor, AppRoles.HiringManager];
  const execRoles: AppRole[] = [AppRoles.Executive, AppRoles.ProgramOps];

  const canViewPipeline = roles.some((r) => pipelineRoles.includes(r));
  const canApprove = roles.includes(AppRoles.ProgramOps);
  const canViewExec = roles.some((r) => execRoles.includes(r));
  const isCandidate = roles.includes(AppRoles.Candidate);

  return (
    <ul className={styles.list}>
      <li><Link to="/">Home</Link></li>
      {canViewPipeline && <li><Link to="/pipeline">Talent Pipeline</Link></li>}
      {canApprove && <li><Link to="/ops/approvals">Approval Queue</Link></li>}
      {canViewExec && <li><Link to="/exec">Executive Dashboard</Link></li>}
      {isCandidate && <li><Link to="/profile">My Profile</Link></li>}
    </ul>
  );
}
