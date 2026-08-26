import { Badge, Button, makeStyles, tokens } from '@fluentui/react-components';
import { DismissRegular } from '@fluentui/react-icons';

const useStyles = makeStyles({
  bar: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    marginBottom: tokens.spacingVerticalM,
  },
});

export function HashtagFilterBar({ hashtag, onClear }: { hashtag: string; onClear: () => void }) {
  const styles = useStyles();
  return (
    <div className={styles.bar}>
      <Badge appearance="filled" color="brand">#{hashtag}</Badge>
      <Button appearance="subtle" size="small" icon={<DismissRegular />} onClick={onClear}>
        Back to all posts
      </Button>
    </div>
  );
}
