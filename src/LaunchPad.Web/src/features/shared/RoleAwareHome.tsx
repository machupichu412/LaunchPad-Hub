import { useMsal } from '@azure/msal-react';
import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Badge,
  Body1,
  Card,
  Title1,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { RocketRegular } from '@fluentui/react-icons';
import { useApiTokenDiagnostics } from '../../auth/useApiTokenDiagnostics';
import { roleLabel, type AppRole } from '../../auth/roles';

const useStyles = makeStyles({
  banner: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalL,
    borderRadius: tokens.borderRadiusLarge,
    padding: tokens.spacingVerticalXXL,
    backgroundColor: tokens.colorBrandBackground2,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorBrandStroke2}`,
    marginBottom: tokens.spacingVerticalXL,
  },
  bannerIcon: {
    display: 'flex',
    flexShrink: 0,
    alignItems: 'center',
    justifyContent: 'center',
    width: '56px',
    height: '56px',
    borderRadius: tokens.borderRadiusCircular,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    fontSize: '28px',
  },
  bannerTitle: {
    color: tokens.colorBrandForeground2,
  },
  roleRow: {
    display: 'flex',
    gap: tokens.spacingHorizontalXS,
    marginTop: tokens.spacingVerticalS,
  },
  diagnosticsCard: {
    padding: tokens.spacingVerticalM,
  },
  pre: {
    fontSize: tokens.fontSizeBase200,
    fontFamily: tokens.fontFamilyMonospace,
    backgroundColor: tokens.colorNeutralBackground3,
    borderRadius: tokens.borderRadiusMedium,
    padding: tokens.spacingVerticalM,
    overflow: 'auto',
    color: tokens.colorNeutralForeground1,
  },
});

export function RoleAwareHome() {
  const styles = useStyles();
  const { accounts } = useMsal();
  const { roles, spaIdTokenClaims, apiAccessTokenClaims } = useApiTokenDiagnostics();
  const firstName = (accounts[0]?.name ?? 'there').split(' ')[0];

  return (
    <>
      <div className={styles.banner}>
        <span className={styles.bannerIcon} aria-hidden="true">
          <RocketRegular />
        </span>
        <div>
          <Title1 className={styles.bannerTitle}>Welcome to LaunchPad, {firstName}</Title1>
          <Body1>Use the role switcher in the header to change perspective if you hold more than one role.</Body1>
          <div className={styles.roleRow}>
            {roles.length > 0 ? (
              roles.map((role) => (
                <Badge key={role} appearance="tint" color="brand">
                  {roleLabel(role as AppRole)}
                </Badge>
              ))
            ) : (
              <Badge appearance="tint" color="warning">
                No roles assigned yet
              </Badge>
            )}
          </div>
        </div>
      </div>

      <Accordion collapsible>
        <AccordionItem value="diagnostics">
          <AccordionHeader>Token diagnostics</AccordionHeader>
          <AccordionPanel>
            <Card className={styles.diagnosticsCard}>
              <Body1>
                <strong>SPA ID token claims</strong>
              </Body1>
              <pre className={styles.pre}>{JSON.stringify(spaIdTokenClaims, null, 2)}</pre>
              <Body1>
                <strong>API access token claims</strong>
              </Body1>
              <pre className={styles.pre}>{JSON.stringify(apiAccessTokenClaims, null, 2)}</pre>
            </Card>
          </AccordionPanel>
        </AccordionItem>
      </Accordion>
    </>
  );
}
