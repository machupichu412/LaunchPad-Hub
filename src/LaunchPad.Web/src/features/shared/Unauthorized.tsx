import { Title2, Body1 } from '@fluentui/react-components';

export function Unauthorized() {
  return (
    <>
      <Title2>Not authorized</Title2>
      <Body1>Your account doesn't have access to this page. Contact Program Ops if this seems wrong.</Body1>
    </>
  );
}
