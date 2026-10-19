import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { AppRoles, type AppRole } from '../../auth/roles';
import { ProjectSupport } from './ProjectSupport';

const state = vi.hoisted(() => ({ role: 'LaunchPad.SubjectMatterExpert' as AppRole, requests: vi.fn() }));
vi.mock('../../auth/ActiveRoleContext', () => ({ useActiveRole: () => ({ activeRole: state.role }) }));
vi.mock('../../api/projectSupport', () => ({ supportRequest: (...args: unknown[]) => state.requests(...args), downloadSupportFile: vi.fn() }));
const question = { projectQuestionId: 1, title: 'How to cache?', body: 'Help with the API', authorName: 'Candidate', createdUtc: '2026-10-05T12:00:00Z', deliverableId: null, answers: [] };
function setup() {
  const cache = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(<QueryClientProvider client={cache}><ProjectSupport /></QueryClientProvider>);
  return userEvent.setup();
}
beforeEach(() => {
  state.role = AppRoles.SubjectMatterExpert;
  state.requests.mockReset().mockImplementation(async (path: string, init?: RequestInit) => {
    if (init) return {};
    if (path === '/projects') return [{ projectId: 1, name: 'Dashboard', description: 'A dashboard project' }];
    if (path.endsWith('/questions')) return [question];
    if (path.endsWith('/deliverables')) return [{ deliverableId: 2, title: 'Prototype', fileName: 'prototype.pdf', candidateName: 'Candidate' }];
    return [];
  });
});
describe('Project support workflows', () => {
  it('shows an unassigned SME a clear empty state', async () => {
    state.requests.mockResolvedValue([]);
    setup();
    expect(await screen.findByText(/No projects assigned yet/)).toBeInTheDocument();
  });
  it('lets an SME respond with text and an attachment and start a deliverable review', async () => {
    const user = setup();
    await screen.findByRole('option', { name: 'Dashboard' });
    await user.selectOptions(screen.getByLabelText('Project'), '1');
    await screen.findByText('How to cache?');
    await user.type(screen.getByLabelText('Reply'), 'Cache per project.');
    await user.upload(screen.getByLabelText(/Attach image or file/), new File(['notes'], 'notes.txt', { type: 'text/plain' }));
    await user.click(screen.getByRole('button', { name: 'Post reply' }));
    const call = state.requests.mock.calls.find(([path, init]) => path.endsWith('/answers') && init?.method === 'POST');
    expect(call?.[1].body.get('body')).toBe('Cache per project.');
    expect(call?.[1].body.get('file').name).toBe('notes.txt');
    await user.click(screen.getByRole('button', { name: 'Review deliverable' }));
    await user.type(screen.getByLabelText(/Technical feedback/), 'Add error handling.');
    await user.click(screen.getByRole('button', { name: 'Post review' }));
    expect(state.requests).toHaveBeenCalledWith('/projects/1/questions', expect.objectContaining({ body: JSON.stringify({ title: 'Review: Prototype', body: 'Add error handling.', deliverableId: 2 }) }));
  });
  it('lets candidates ask questions without showing SME assignment controls', async () => {
    state.role = AppRoles.Candidate;
    const user = setup();
    await screen.findByRole('option', { name: 'Dashboard' });
    await user.selectOptions(screen.getByLabelText('Project'), '1');
    await user.type(await screen.findByLabelText(/^Title/), 'API question');
    await user.type(screen.getByLabelText(/^Question/), 'How does caching work?');
    await user.click(screen.getByRole('button', { name: 'Ask question' }));
    expect(state.requests).toHaveBeenCalledWith('/projects/1/questions', expect.objectContaining({ method: 'POST' }));
    expect(screen.queryByLabelText('Assign SME')).not.toBeInTheDocument();
    expect(screen.queryByText('Project deliverables')).not.toBeInTheDocument();
  });
});
