import { backendPath, isWebSocketPrefix } from './backendPaths';

describe('backendPath', () => {
  it('passes /api and /hub on without their prefix', () => {
    expect(backendPath('/api', '/api/seasons?x=1')).toBe('/seasons?x=1');
    expect(backendPath('/api', '/api')).toBe('/');
    expect(backendPath('/hub', '/hub/TeamsHub/negotiate')).toBe('/TeamsHub/negotiate');
  });

  it('passes the Discord callback on as it is', () => {
    expect(backendPath('/signin-discord', '/signin-discord?code=1')).toBe('/signin-discord?code=1');
  });

  it('passes the hubs\' page on as it is, and its hub connections to the hub', () => {
    expect(backendPath('/signalr-dev', '/signalr-dev')).toBe('/signalr-dev');
    expect(backendPath('/signalr-dev', '/signalr-dev/index.html')).toBe('/signalr-dev/index.html');
    expect(backendPath('/signalr-dev', '/signalr-dev/spec.json')).toBe('/signalr-dev/spec.json');
    expect(backendPath('/signalr-dev', '/signalr-dev/_next/static/chunks/main.js')).toBe('/signalr-dev/_next/static/chunks/main.js');
    expect(backendPath('/signalr-dev', '/signalr-dev/TeamsHub')).toBe('/TeamsHub');
    expect(backendPath('/signalr-dev', '/signalr-dev/TeamsHub/negotiate?negotiateVersion=1')).toBe('/TeamsHub/negotiate?negotiateVersion=1');
    expect(backendPath('/signalr-dev', '/signalr-dev/QueueHub?id=abc')).toBe('/QueueHub?id=abc');
    expect(backendPath('/signalr-dev', '/signalr-dev/Hubris.html')).toBe('/signalr-dev/Hubris.html');
  });

  it('lets SignalR upgrade only the hubs and the hubs\' page', () => {
    expect(isWebSocketPrefix('/hub')).toBeTrue();
    expect(isWebSocketPrefix('/signalr-dev')).toBeTrue();
    expect(isWebSocketPrefix('/api')).toBeFalse();
    expect(isWebSocketPrefix('/signin-discord')).toBeFalse();
  });
});
