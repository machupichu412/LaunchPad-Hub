import { Body1, Button, Caption1, Card, Title2, makeStyles, tokens } from '@fluentui/react-components';
import { DEV_PERSONAS, setActivePersona } from './devPersonas';

const useStyles = makeStyles({
  root: {
    maxWidth: '720px',
    margin: '0 auto',
    padding: `${tokens.spacingVerticalXXL} ${tokens.spacingHorizontalL}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
    gap: tokens.spacingHorizontalM,
  },
});

/** Local dev only (see devPersonas.ts). Stands in for the Entra sign-in page. */
export function PersonaPicker() {
  const styles = useStyles();
  return (
    <main className={styles.root}>
      <Title2>Sign in as a test persona</Title2>
      <Body1>Local development only. Each browser tab keeps its own persona.</Body1>
      <div className={styles.grid}>
        {DEV_PERSONAS.map((p) => (
          <Card key={p.key}>
            <Body1>
              <strong>{p.displayName}</strong>
            </Body1>
            <Caption1>{p.note}</Caption1>
            <Button appearance="primary" onClick={() => setActivePersona(p.key)} data-persona={p.key}>
              Sign in as {p.displayName.split(' ')[0]}
            </Button>
          </Card>
        ))}
      </div>
    </main>
  );
}
