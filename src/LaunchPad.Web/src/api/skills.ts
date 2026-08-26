import { authedFetch } from './authedFetch';
import type { CreateSkillRequest, SkillCategoryDto, SkillDto } from './types';

export async function getSkills(): Promise<SkillDto[]> {
  const response = await authedFetch('/api/skills');
  if (!response.ok) throw new Error(`Failed to load skills: ${response.status}`);
  return response.json() as Promise<SkillDto[]>;
}

export async function getSkillCategories(): Promise<SkillCategoryDto[]> {
  const response = await authedFetch('/api/skills/categories');
  if (!response.ok) throw new Error(`Failed to load skill categories: ${response.status}`);
  return response.json() as Promise<SkillCategoryDto[]>;
}

export async function createSkill(request: CreateSkillRequest): Promise<SkillDto> {
  const response = await authedFetch('/api/skills', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to add skill: ${response.status}`);
  return response.json() as Promise<SkillDto>;
}

export async function deleteSkill(skillId: number): Promise<void> {
  const response = await authedFetch(`/api/skills/${skillId}`, { method: 'DELETE' });
  if (response.status === 409) {
    const body = await response.text();
    throw new Error(body.replace(/^"|"$/g, '') || 'This skill is still in use and can\'t be removed.');
  }
  if (!response.ok) throw new Error(`Failed to remove skill: ${response.status}`);
}
