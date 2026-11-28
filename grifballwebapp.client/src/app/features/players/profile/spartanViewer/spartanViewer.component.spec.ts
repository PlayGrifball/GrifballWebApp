import { ComponentFixture, TestBed } from '@angular/core/testing';
import { createSpartanScene, releaseModel, SPARTAN_SCENE, SpartanScene, SpartanSceneFactory } from './spartanScene';
import { SpartanViewerComponent } from './spartanViewer.component';

describe('SpartanViewerComponent', () => {
  let shown: string[];
  let played: string[];
  let disposed: number;
  let scene: SpartanScene;

  beforeEach(() => {
    shown = [];
    played = [];
    disposed = 0;
    scene = { show: async url => { shown.push(url); return ["neutral", "stance"]; }, play: clip => { played.push(clip); }, dispose: () => { disposed++; } };
  });

  function create(factory: SpartanSceneFactory, url = '/a.glb'): ComponentFixture<SpartanViewerComponent> {
    TestBed.configureTestingModule({ providers: [{ provide: SPARTAN_SCENE, useValue: factory }] });
    const fixture = TestBed.createComponent(SpartanViewerComponent);
    fixture.componentRef.setInput('url', url);
    fixture.detectChanges();
    return fixture;
  }
  async function settle(f: ComponentFixture<SpartanViewerComponent>): Promise<void> {
    for (let i = 0; i < 3; i++) {
      await f.whenStable();
      f.detectChanges();
      await new Promise<void>(resolve => setTimeout(resolve));
    }
  }
  const text = (f: ComponentFixture<SpartanViewerComponent>) => (f.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');

  it('says it is loading, then shows the model in the canvas it made the scene for', async () => {
    let canvas: HTMLCanvasElement | undefined;
    const fixture = create(async c => { canvas = c; return scene; });
    expect(text(fixture)).toContain('Loading the 3D model');

    await settle(fixture);

    expect(canvas).toBe((fixture.nativeElement as HTMLElement).querySelector('canvas')!);
    expect(shown).toEqual(['/a.glb']);
    expect(fixture.componentInstance.state()).toBe('ready');
    expect(text(fixture)).not.toContain('3D model');
  });

  it('poses the model with the clip, and again when the clip changes', async () => {
    const fixture = create(async () => scene);
    fixture.componentRef.setInput('clip', 'neutral');
    await settle(fixture);

    fixture.componentRef.setInput('clip', 'stance');
    await settle(fixture);

    expect(played).toContain('neutral');
    expect(played.at(-1)).toBe('stance');
  });

  it('tells which clips the model has once it is shown', async () => {
    const fixture = create(async () => scene);
    const named: (readonly string[])[] = [];
    fixture.componentInstance.clipNames.subscribe(names => named.push(names));
    await settle(fixture);

    expect(named).toEqual([['neutral', 'stance']]);
  });

  it('tells there are no clips when the next model cannot be read', async () => {
    const fixture = create(async () => scene);
    const named: (readonly string[])[] = [];
    fixture.componentInstance.clipNames.subscribe(names => named.push(names));
    await settle(fixture);

    scene.show = async () => { throw new Error('bad glb'); };
    fixture.componentRef.setInput('url', '/b.glb');
    await settle(fixture);

    expect(named).toEqual([['neutral', 'stance'], []]);
  });

  it('ignores a slow model that fails after a newer one was shown', async () => {
    const pending: { resolve: (names: string[]) => void; reject: (e: Error) => void }[] = [];
    scene.show = url => { shown.push(url); return new Promise((resolve, reject) => pending.push({ resolve, reject })); };
    const fixture = create(async () => scene);
    const named: (readonly string[])[] = [];
    fixture.componentInstance.clipNames.subscribe(names => named.push(names));
    await settle(fixture);
    fixture.componentRef.setInput('url', '/b.glb');
    await settle(fixture);

    pending[1].resolve(['neutral', 'stance']);
    pending[0].reject(new Error('bad glb'));
    await settle(fixture);

    expect(named).toEqual([['neutral', 'stance']]);
    expect(fixture.componentInstance.state()).toBe('ready');
  });

  it('tells only the clips of the newest model when a slow one is overtaken', async () => {
    const pending: ((names: string[]) => void)[] = [];
    scene.show = url => { shown.push(url); return new Promise(resolve => pending.push(resolve)); };
    const fixture = create(async () => scene);
    const named: (readonly string[])[] = [];
    fixture.componentInstance.clipNames.subscribe(names => named.push(names));
    await settle(fixture);
    fixture.componentRef.setInput('url', '/b.glb');
    await settle(fixture);

    pending[1](['neutral', 'Ruthless War Club']);
    pending[0](['neutral', 'stance']);
    await settle(fixture);

    expect(shown).toEqual(['/a.glb', '/b.glb']);
    expect(named).toEqual([['neutral', 'Ruthless War Club']]);
  });

  it('shows the next model when the address changes', async () => {
    const fixture = create(async () => scene);
    await settle(fixture);

    fixture.componentRef.setInput('url', '/b.glb');
    await settle(fixture);

    expect(shown).toEqual(['/a.glb', '/b.glb']);
  });

  it('says so when the browser cannot draw it', async () => {
    const fixture = create(async () => { throw new Error('no WebGL'); });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.state()).toBe('unavailable');
    expect(text(fixture)).toContain('The 3D model cannot be shown here.');
  });

  it('says so when the model cannot be read', async () => {
    scene.show = async () => { throw new Error('bad glb'); };
    const fixture = create(async () => scene);
    await settle(fixture);

    expect(fixture.componentInstance.state()).toBe('unavailable');
  });

  it('lets go of the scene when it goes away', async () => {
    const fixture = create(async () => scene);
    await settle(fixture);

    fixture.destroy();

    expect(disposed).toBe(1);
  });

  it('lets go of a scene that was made after it went away', async () => {
    let finish!: (s: SpartanScene) => void;
    const fixture = create(() => new Promise<SpartanScene>(resolve => { finish = resolve; }));
    await Promise.resolve();
    fixture.destroy();

    finish(scene);
    await Promise.resolve();
    await Promise.resolve();

    expect(disposed).toBe(1);
    expect(shown).toEqual([]);
  });
});

describe('releaseModel', () => {
  it('takes the model out of the scene and frees what it holds, textures included', async () => {
    const THREE = await import('three');
    const scene = new THREE.Scene();
    const texture = new THREE.Texture();
    const material = new THREE.MeshStandardMaterial({ map: texture });
    const geometry = new THREE.BufferGeometry();
    const group = new THREE.Group();
    group.add(new THREE.Mesh(geometry, [material]), new THREE.Mesh(geometry, material), new THREE.Group());
    scene.add(group);
    const freed = { texture: 0, material: 0, geometry: 0 };
    texture.addEventListener('dispose', () => freed.texture++);
    material.addEventListener('dispose', () => freed.material++);
    geometry.addEventListener('dispose', () => freed.geometry++);

    releaseModel(THREE, scene, group);

    expect(scene.children).toEqual([]);
    expect(freed.texture).toBeGreaterThan(0);
    expect(freed.material).toBeGreaterThan(0);
    expect(freed.geometry).toBeGreaterThan(0);
  });

  it('is what the scene token gives by default', () => {
    expect(TestBed.inject(SPARTAN_SCENE)).toBe(createSpartanScene);
  });
});

describe('createSpartanScene', () => {
  const webgl = (() => { try { return !!document.createElement('canvas').getContext('webgl2'); } catch { return false; } })();

  // A one-triangle glTF, as a data address
  const triangleWith = (clip: string | null): string => {
    const bytes = new Uint8Array(new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 5, 0, 0]).buffer);
    const animated = clip !== null;
    const gltf = {
      asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }],
      meshes: [{ primitives: [{ attributes: { POSITION: 0 } }] }],
      accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [0, 0, 0], max: [1, 1, 0] }, ...(animated ? [{ bufferView: 0, byteOffset: 36, componentType: 5126, count: 1, type: 'SCALAR', min: [0], max: [0] }, { bufferView: 0, byteOffset: 40, componentType: 5126, count: 1, type: 'VEC3' }] : [])],
      ...(animated ? { animations: [{ name: clip, samplers: [{ input: 1, output: 2 }], channels: [{ sampler: 0, target: { node: 0, path: 'translation' } }] }] } : {}),
      bufferViews: [{ buffer: 0, byteLength: 52 }],
      buffers: [{ byteLength: 52, uri: `data:application/octet-stream;base64,${btoa(String.fromCharCode(...bytes))}` }],
    };
    return `data:model/gltf+json;base64,${btoa(JSON.stringify(gltf))}`;
  };
  const triangle = triangleWith(null);

  it('shows a model and lets go of it, where the browser can draw WebGL', async () => {
    if (!webgl) { pending('This browser has no WebGL.'); return; }
    const canvas = document.createElement('canvas');
    document.body.appendChild(canvas);
    const scene = await createSpartanScene(canvas);

    await scene.show(triangle);
    await scene.show(triangle);
    scene.dispose();
    canvas.remove();

    expect(canvas.isConnected).toBeFalse();
  }, 30000);

  it('adds only the newest model, and none once it is disposed', async () => {
    if (!webgl) { pending('This browser has no WebGL.'); return; }
    const THREE = await import('three');
    const add = spyOn(THREE.Scene.prototype, 'add').and.callThrough();
    const canvas = document.createElement('canvas');
    const scene = await createSpartanScene(canvas);

    const before = add.calls.count();
    // The older model is never shown, and the newer one has no clips
    expect(await Promise.all([scene.show(triangle), scene.show(triangle)])).toEqual([[], []]);
    const afterBoth = add.calls.count();
    expect(afterBoth - before).toBe(1);

    const late = scene.show(triangle);
    scene.dispose();
    await late;
    expect(add.calls.count()).toBe(afterBoth);
  }, 30000);

  it('poses the model with the clip asked for, before or after it loads, and leaves it be for a clip it lacks', async () => {
    if (!webgl) { pending('This browser has no WebGL.'); return; }
    const THREE = await import('three');
    const stop = spyOn(THREE.AnimationMixer.prototype, 'stopAllAction').and.callThrough();
    const canvas = document.createElement('canvas');
    const scene = await createSpartanScene(canvas);

    scene.play('stance');
    expect(await scene.show(triangleWith('stance'))).toEqual(['stance']);
    expect(stop).toHaveBeenCalledTimes(1);
    scene.play('stance');
    expect(stop).toHaveBeenCalledTimes(2);
    scene.play('missing');
    expect(stop).toHaveBeenCalledTimes(2);
    scene.dispose();
  }, 30000);

  it('refuses a model that is not one', async () => {
    if (!webgl) { pending('This browser has no WebGL.'); return; }
    const canvas = document.createElement('canvas');
    const scene = await createSpartanScene(canvas);

    await expectAsync(scene.show('data:model/gltf+json;base64,e30=')).toBeRejected();
    scene.dispose();
  }, 30000);
});
