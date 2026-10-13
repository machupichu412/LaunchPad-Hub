import { authedFetch } from './authedFetch';
export interface SupportProject { projectId: number; name: string; description: string | null }
export interface Expert { appUserId: number; displayName: string }
export interface Answer { projectAnswerId: number; authorName: string; body: string; createdUtc: string; fileName: string | null }
export interface Question { projectQuestionId: number; title: string; body: string; authorName: string; createdUtc: string; deliverableId: number | null; answers: Answer[] }
export interface SupportDeliverable { deliverableId: number; title: string; fileName: string; candidateName: string; submittedUtc: string }
const root = '/api/project-support';
export async function supportRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await authedFetch(`${root}${path}`, init);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail && !detail.startsWith('{') ? detail : `Request failed (${response.status}). Please try again.`);
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
export async function downloadSupportFile(path: string, name: string) {
  const response = await authedFetch(`${root}${path}`);
  if (!response.ok) throw new Error(`Unable to download file (${response.status}).`);
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url; link.download = name; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
