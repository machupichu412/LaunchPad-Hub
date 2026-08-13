import { authedFetch } from './authedFetch';
import type { NotificationDto } from './types';

export async function getMyNotifications(): Promise<NotificationDto[]> {
  const response = await authedFetch('/api/notifications');
  if (!response.ok) throw new Error(`Failed to load notifications: ${response.status}`);
  return response.json() as Promise<NotificationDto[]>;
}

export async function getUnreadNotificationCount(): Promise<number> {
  const response = await authedFetch('/api/notifications/unread-count');
  if (!response.ok) throw new Error(`Failed to load unread count: ${response.status}`);
  return response.json() as Promise<number>;
}

export async function markNotificationRead(notificationId: number): Promise<void> {
  const response = await authedFetch(`/api/notifications/${notificationId}/read`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to mark notification read: ${response.status}`);
}

export async function markAllNotificationsRead(): Promise<void> {
  const response = await authedFetch('/api/notifications/read-all', { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to mark all notifications read: ${response.status}`);
}
