import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Field, Input, Select, Spinner, Textarea, Title1, Title3, Body1, makeStyles, tokens } from '@fluentui/react-components';
import { AppRoles } from '../../auth/roles';
import { useActiveRole } from '../../auth/ActiveRoleContext';
import { supportRequest, downloadSupportFile, type SupportProject, type Expert, type Question, type SupportDeliverable } from '../../api/projectSupport';

const useStyles = makeStyles({
  stack: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  row: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalM, alignItems: 'center' },
  message: { whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' },
});

export function ProjectSupport() {
  const styles = useStyles();
  const { activeRole } = useActiveRole();
  const [selected, setSelected] = useState('');
  const projects = useQuery({ queryKey: ['support', 'projects', activeRole], queryFn: () => supportRequest<SupportProject[]>('/projects') });
  const project = projects.data?.find(p => String(p.projectId) === selected);
  return <div className={styles.stack}>
    <Title1>{activeRole === AppRoles.SubjectMatterExpert ? 'My Projects & Questions' : 'Project Support'}</Title1>
    <Body1>Connect candidates with Subject Matter Experts (SMEs) for technical questions and deliverable feedback.</Body1>
    {projects.isLoading && <Spinner label="Loading projects" />}
    {projects.error && <p role="alert">{projects.error.message}</p>}
    {projects.data?.length === 0 && <Card>No projects assigned yet. Program Ops can assign SMEs to projects.</Card>}
    {!!projects.data?.length && <Field label="Project"><Select value={selected} onChange={(_, d) => setSelected(d.value)}>
      <option value="">Select a project</option>
      {projects.data.map(p => <option key={p.projectId} value={p.projectId}>{p.name}</option>)}
    </Select></Field>}
    {project && <ProjectWorkspace key={`${activeRole}-${project.projectId}`} project={project} />}
  </div>;
}

function ProjectWorkspace({ project }: { project: SupportProject }) {
  const styles = useStyles();
  const { activeRole } = useActiveRole();
  const ops = activeRole === AppRoles.ProgramOps;
  const sme = activeRole === AppRoles.SubjectMatterExpert;
  const candidate = activeRole === AppRoles.Candidate;
  const base = `/projects/${project.projectId}`;
  const cache = useQueryClient();
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [review, setReview] = useState<SupportDeliverable | null>(null);
  const [expert, setExpert] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const experts = useQuery({ queryKey: ['support', project.projectId, 'experts'], queryFn: () => supportRequest<Expert[]>(`${base}/experts`) });
  const directory = useQuery({ queryKey: ['support', 'experts'], queryFn: () => supportRequest<Expert[]>('/experts'), enabled: ops });
  const questions = useQuery({ queryKey: ['support', project.projectId, 'questions'], queryFn: () => supportRequest<Question[]>(`${base}/questions`) });
  const deliverables = useQuery({ queryKey: ['support', project.projectId, 'deliverables'], queryFn: () => supportRequest<SupportDeliverable[]>(`${base}/deliverables`), enabled: sme || ops });
  const assign = useMutation({
    mutationFn: ({ id, remove }: { id: number; remove: boolean }) => supportRequest<void>(`${base}/experts/${id}`, { method: remove ? 'DELETE' : 'PUT' }),
    onSuccess: () => { setExpert(''); void cache.invalidateQueries({ queryKey: ['support', project.projectId, 'experts'] }); },
  });
  const ask = useMutation({
    mutationFn: () => supportRequest(`${base}/questions`, { method: 'POST', body: JSON.stringify({ title, body, deliverableId: review?.deliverableId }) }),
    onSuccess: () => { setTitle(''); setBody(''); setReview(null); void cache.invalidateQueries({ queryKey: ['support', project.projectId, 'questions'] }); },
  });
  async function download(path: string, name: string) {
    setDownloadError('');
    try { await downloadSupportFile(path, name); } catch (e) { setDownloadError((e as Error).message); }
  }
  return <div className={styles.stack}>
    <Title3>{project.name}</Title3><Body1 className={styles.message}>{project.description}</Body1>
    <Card><Title3>Subject Matter Experts</Title3>
      {experts.isLoading && <Spinner size="tiny" label="Loading SMEs" />}
      {experts.data?.length === 0 && <Body1>No SMEs assigned yet. Questions can still be posted.</Body1>}
      {experts.data?.map(e => <div className={styles.row} key={e.appUserId}><span>{e.displayName}</span>{ops && <Button disabled={assign.isPending} onClick={() => assign.mutate({ id: e.appUserId, remove: true })}>Remove {e.displayName}</Button>}</div>)}
      {ops && <>
        <Body1>SMEs appear here after their first sign-in with the Subject Matter Expert role.</Body1>
        <Field label="Assign SME"><Select value={expert} onChange={(_, d) => setExpert(d.value)}><option value="">Select an SME</option>
          {directory.data?.filter(e => !experts.data?.some(a => a.appUserId === e.appUserId)).map(e => <option key={e.appUserId} value={e.appUserId}>{e.displayName}</option>)}
        </Select></Field>
        <Button disabled={!expert || assign.isPending} onClick={() => assign.mutate({ id: Number(expert), remove: false })}>Assign to project</Button>
      </>}
    </Card>
    {(sme || ops) && <Card><Title3>Project deliverables</Title3>
      {deliverables.isLoading && <Spinner label="Loading deliverables" />}
      {deliverables.data?.length === 0 && <Body1>No deliverables submitted yet.</Body1>}
      {deliverables.data?.map(d => <div className={styles.row} key={d.deliverableId}>
        <span>{d.title} — {d.candidateName}</span>
        <Button onClick={() => void download(`${base}/deliverables/${d.deliverableId}/file`, d.fileName)}>Download {d.fileName}</Button>
        {sme && <Button onClick={() => { setReview(d); setTitle(`Review: ${d.title}`.slice(0, 200)); setBody(''); }}>Review deliverable</Button>}
      </div>)}
    </Card>}
    {(candidate || review) && <Card><Title3>{review ? `Feedback on ${review.title}` : 'Ask a technical question'}</Title3>
      <Field label="Title" required><Input value={title} maxLength={200} onChange={(_, d) => setTitle(d.value)} /></Field>
      <Field label={review ? 'Technical feedback' : 'Question'} required><Textarea value={body} maxLength={10000} resize="vertical" onChange={(_, d) => setBody(d.value)} /></Field>
      <div className={styles.row}><Button appearance="primary" disabled={!title.trim() || !body.trim() || ask.isPending} onClick={() => ask.mutate()}>{ask.isPending ? 'Posting…' : review ? 'Post review' : 'Ask question'}</Button>
      {review && <Button onClick={() => { setReview(null); setTitle(''); setBody(''); }}>Cancel review</Button>}</div>
    </Card>}
    {[experts.error, directory.error, questions.error, deliverables.error, assign.error, ask.error].filter(Boolean).map((e, i) => <p role="alert" key={i}>{e!.message}</p>)}
    {downloadError && <p role="alert">{downloadError}</p>}
    <Title3>Questions & deliverable reviews</Title3>
    {questions.isLoading && <Spinner label="Loading discussions" />}
    {questions.data?.length === 0 && <Body1>No questions or reviews yet.</Body1>}
    {questions.data?.map(q => <Discussion key={q.projectQuestionId} projectId={project.projectId} question={q} canReply={sme || candidate} />)}
  </div>;
}

function Discussion({ projectId, question, canReply }: { projectId: number; question: Question; canReply: boolean }) {
  const styles = useStyles();
  const cache = useQueryClient();
  const [body, setBody] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [inputKey, setInputKey] = useState(0);
  const [error, setError] = useState('');
  const base = `/projects/${projectId}/questions/${question.projectQuestionId}/answers`;
  const reply = useMutation({
    mutationFn: () => { const data = new FormData(); data.set('body', body); if (file) data.set('file', file); return supportRequest(base, { method: 'POST', body: data }); },
    onSuccess: () => { setBody(''); setFile(null); setInputKey(k => k + 1); void cache.invalidateQueries({ queryKey: ['support', projectId, 'questions'] }); },
  });
  async function download(answerId: number, name: string) {
    setError('');
    try { await downloadSupportFile(`${base}/${answerId}/file`, name); } catch (e) { setError((e as Error).message); }
  }
  return <Card>
    <Title3>{question.deliverableId ? 'Deliverable review · ' : ''}{question.title}</Title3>
    <Body1>{question.authorName} · {new Date(question.createdUtc).toLocaleString()}</Body1>
    <p className={styles.message}>{question.body}</p>
    {question.answers.map(a => <Card key={a.projectAnswerId}>
      <Body1><strong>{a.authorName}</strong> · {new Date(a.createdUtc).toLocaleString()}</Body1>
      {a.body && <p className={styles.message}>{a.body}</p>}
      {a.fileName && <Button onClick={() => void download(a.projectAnswerId, a.fileName!)}>Download {a.fileName}</Button>}
    </Card>)}
    {canReply && <>
      <Field label="Reply"><Textarea value={body} maxLength={10000} onChange={(_, d) => setBody(d.value)} resize="vertical" /></Field>
      <Field label={{ children: "Attach image or file (up to 10 MB)", htmlFor: `support-file-${question.projectQuestionId}` }}><input id={`support-file-${question.projectQuestionId}`} key={inputKey} type="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv,.rtf,.png,.jpg,.jpeg,.gif,.webp,.zip" onChange={e => {
        const chosen = e.target.files?.[0] ?? null;
        if (chosen && chosen.size > 10 * 1024 * 1024) { setError('Attachments must be 10 MB or smaller.'); setFile(null); e.target.value = ''; }
        else { setFile(chosen); setError(''); }
      }} /></Field>
      <Button appearance="primary" disabled={(!body.trim() && !file) || reply.isPending} onClick={() => reply.mutate()}>{reply.isPending ? 'Posting…' : 'Post reply'}</Button>
    </>}
    {(error || reply.error) && <p role="alert">{error || reply.error?.message}</p>}
  </Card>;
}
