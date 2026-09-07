import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  Body1,
  Button,
  Caption1,
  Card,
  Divider,
  Input,
  Subtitle1,
  Subtitle2,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components';
import {
  ArrowLeftRegular,
  ArrowRightRegular,
  CheckmarkCircleRegular,
  CloudRegular,
  CompassNorthwestRegular,
  FolderRegular,
  InfoRegular,
  PeopleTeamRegular,
  QuestionCircleRegular,
  SearchRegular,
  TagRegular,
  WarningRegular,
} from '@fluentui/react-icons';
import { PageHeader } from '../../../components/PageHeader';
import { useSurfaceStyles } from '../../../theme/surfaces';
import { articleSearchText, findArticle, helpArticles } from './helpContent';
import type { HelpArticle, HelpBlock } from './helpContent';

const articleIcons = {
  compass: CompassNorthwestRegular,
  people: PeopleTeamRegular,
  folder: FolderRegular,
  checkmark: CheckmarkCircleRegular,
  tag: TagRegular,
  cloud: CloudRegular,
  warning: WarningRegular,
  question: QuestionCircleRegular,
} as const;

const useStyles = makeStyles({
  search: {
    maxWidth: '420px',
    marginBottom: tokens.spacingVerticalXL,
  },
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  cardLink: {
    textDecorationLine: 'none',
    color: 'inherit',
    display: 'block',
  },
  card: {
    padding: tokens.spacingVerticalL,
    height: '100%',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
    alignItems: 'flex-start',
  },
  cardIcon: {
    display: 'flex',
    fontSize: '24px',
    color: tokens.colorBrandForeground1,
  },
  empty: {
    padding: tokens.spacingVerticalXXL,
    textAlign: 'center',
  },

  // --- Article ---
  backButton: {
    marginBottom: tokens.spacingVerticalM,
  },
  article: {
    maxWidth: '760px',
  },
  block: {
    marginBottom: tokens.spacingVerticalL,
  },
  heading: {
    display: 'block',
    marginTop: tokens.spacingVerticalXXL,
    marginBottom: tokens.spacingVerticalM,
  },
  list: {
    margin: 0,
    paddingLeft: tokens.spacingHorizontalXXL,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    // Tables are the one thing here that can outgrow a narrow window; let this
    // scroll on its own rather than letting the page scroll sideways.
    display: 'table',
  },
  tableScroll: {
    overflowX: 'auto',
  },
  th: {
    textAlign: 'left',
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderBottomWidth: '1px',
    borderBottomStyle: 'solid',
    borderBottomColor: tokens.colorNeutralStroke1,
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    whiteSpace: 'nowrap',
  },
  td: {
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderBottomWidth: '1px',
    borderBottomStyle: 'solid',
    borderBottomColor: tokens.colorNeutralStroke2,
    verticalAlign: 'top',
    fontSize: tokens.fontSizeBase300,
  },
  callout: {
    display: 'flex',
    gap: tokens.spacingHorizontalM,
    padding: tokens.spacingVerticalM,
    borderRadius: tokens.borderRadiusMedium,
    borderLeftWidth: '3px',
    borderLeftStyle: 'solid',
  },
  calloutInfo: {
    backgroundColor: tokens.colorNeutralBackground3,
    borderLeftColor: tokens.colorBrandStroke1,
  },
  calloutCaution: {
    backgroundColor: tokens.colorStatusWarningBackground1,
    borderLeftColor: tokens.colorStatusWarningBorder1,
  },
  calloutIcon: {
    display: 'flex',
    fontSize: '20px',
    flexShrink: 0,
    marginTop: '2px',
  },
  path: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalXS,
    padding: tokens.spacingVerticalS,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground3,
    fontFamily: tokens.fontFamilyMonospace,
    fontSize: tokens.fontSizeBase200,
  },
  pathSeparator: {
    color: tokens.colorNeutralForeground4,
  },
  pathCaption: {
    display: 'block',
    marginTop: tokens.spacingVerticalXS,
  },
  inlineLink: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalXS,
    textDecorationLine: 'none',
  },
  nextNav: {
    marginTop: tokens.spacingVerticalXXL,
  },
});

function BlockRenderer({ block }: { block: HelpBlock }) {
  const styles = useStyles();

  switch (block.kind) {
    case 'paragraph':
      return (
        <Body1 block className={styles.block}>
          {block.text}
        </Body1>
      );

    case 'heading':
      return (
        <Subtitle2 block className={styles.heading}>
          {block.text}
        </Subtitle2>
      );

    case 'bullets':
      return (
        <ul className={mergeClasses(styles.list, styles.block)}>
          {block.items.map((item) => (
            <li key={item}>
              <Body1>{item}</Body1>
            </li>
          ))}
        </ul>
      );

    case 'steps':
      return (
        <ol className={mergeClasses(styles.list, styles.block)}>
          {block.items.map((item) => (
            <li key={item}>
              <Body1>{item}</Body1>
            </li>
          ))}
        </ol>
      );

    case 'path':
      return (
        <div className={styles.block}>
          <div className={styles.path}>
            {block.segments.map((segment, i) => (
              <span key={segment}>
                {i > 0 && <span className={styles.pathSeparator}> / </span>}
                {segment}
              </span>
            ))}
          </div>
          {block.caption && <Caption1 className={styles.pathCaption}>{block.caption}</Caption1>}
        </div>
      );

    case 'table':
      return (
        <div className={mergeClasses(styles.tableScroll, styles.block)}>
          <table className={styles.table}>
            <thead>
              <tr>
                {block.columns.map((column) => (
                  <th key={column} className={styles.th} scope="col">
                    {column}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {block.rows.map((row) => (
                <tr key={row.join('|')}>
                  {row.map((cell, i) => (
                    <td key={`${block.columns[i]}-${cell}`} className={styles.td}>
                      {cell}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );

    case 'callout': {
      const isCaution = block.tone === 'caution';
      return (
        <div
          className={mergeClasses(
            styles.callout,
            isCaution ? styles.calloutCaution : styles.calloutInfo,
            styles.block,
          )}
        >
          <span className={styles.calloutIcon} aria-hidden="true">
            {isCaution ? <WarningRegular /> : <InfoRegular />}
          </span>
          <div>
            <Subtitle2 block>{block.title}</Subtitle2>
            <Body1 block>{block.text}</Body1>
          </div>
        </div>
      );
    }

    case 'link':
      return (
        <Card className={mergeClasses(styles.card, styles.block)}>
          <Link to={block.to} className={styles.inlineLink}>
            <Subtitle2>{block.label}</Subtitle2>
            <ArrowRightRegular />
          </Link>
          <Body1>{block.description}</Body1>
        </Card>
      );
  }
}

function ArticleView({ article }: { article: HelpArticle }) {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const navigate = useNavigate();

  const index = helpArticles.findIndex((a) => a.id === article.id);
  const next = helpArticles[index + 1];

  return (
    <div className={mergeClasses(styles.article, surfaces.fadeInUp)}>
      <Button
        appearance="subtle"
        icon={<ArrowLeftRegular />}
        className={styles.backButton}
        onClick={() => navigate('/ops/help')}
      >
        All guides
      </Button>

      <PageHeader title={article.title} subtitle={article.summary} />

      {article.blocks.map((block, i) => (
        <BlockRenderer key={`${block.kind}-${i}`} block={block} />
      ))}

      {next && (
        <div className={styles.nextNav}>
          <Divider />
          <Card className={mergeClasses(styles.card, surfaces.card)} style={{ marginTop: tokens.spacingVerticalL }}>
            <Caption1>Next guide</Caption1>
            <Link to={`/ops/help/${next.id}`} className={styles.inlineLink}>
              <Subtitle2>{next.title}</Subtitle2>
              <ArrowRightRegular />
            </Link>
          </Card>
        </div>
      )}
    </div>
  );
}

function ArticleIndex() {
  const styles = useStyles();
  const surfaces = useSurfaceStyles();
  const [query, setQuery] = useState('');

  const matches = useMemo(() => {
    const needle = query.trim().toLowerCase();
    if (!needle) return helpArticles;
    return helpArticles.filter((article) => articleSearchText(article).includes(needle));
  }, [query]);

  return (
    <>
      <PageHeader
        title="Program Ops guides"
        subtitle="How to set up and run LaunchPad — cohorts, approvals, skills, and the SharePoint library."
      />

      <Input
        className={styles.search}
        placeholder="Search the guides"
        value={query}
        onChange={(_, data) => setQuery(data.value)}
        contentBefore={<SearchRegular />}
        aria-label="Search the guides"
      />

      {matches.length === 0 ? (
        <Card className={mergeClasses(styles.empty, surfaces.card)}>
          <Body1 block>Nothing matches “{query}”.</Body1>
          <Caption1>Try a word that would appear in a guide, like “cohort”, “skills”, or “SharePoint”.</Caption1>
        </Card>
      ) : (
        <div className={styles.grid}>
          {matches.map((article, i) => {
            const Icon = articleIcons[article.icon];
            return (
              <Link key={article.id} to={`/ops/help/${article.id}`} className={styles.cardLink}>
                <Card
                  className={mergeClasses(styles.card, surfaces.interactive, surfaces.fadeInUp)}
                  style={{ animationDelay: `${i * 40}ms` }}
                >
                  <span className={styles.cardIcon} aria-hidden="true">
                    <Icon />
                  </span>
                  <Subtitle1>{article.title}</Subtitle1>
                  <Body1>{article.summary}</Body1>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
    </>
  );
}

/**
 * In-app documentation for Program Ops — deliberately part of the app rather than
 * a separate wiki, so a guide is one click from the screen it describes and can't
 * drift out of sync behind a link nobody owns.
 *
 * Everything here is static content (see helpContent.ts) — no API calls, nothing
 * role-sensitive beyond the route guard, so it renders instantly and works even
 * when the rest of the app is having a bad day.
 */
export function HelpCenter() {
  const { topicId } = useParams();
  const article = findArticle(topicId);

  // An unknown/stale topic id falls back to the index rather than a dead end —
  // guide ids appear in bookmarks and in messages Ops send each other.
  if (topicId && !article) return <ArticleIndex />;
  if (article) return <ArticleView article={article} />;
  return <ArticleIndex />;
}
