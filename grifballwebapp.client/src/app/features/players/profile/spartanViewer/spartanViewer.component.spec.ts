import { ComponentFixture, TestBed } from '@angular/core/testing';
import { createSpartanScene, SPARTAN_SCENE, SpartanScene, SpartanSceneFactory } from './spartanScene';
import { SpartanViewerComponent } from './spartanViewer.component';

describe('SpartanViewerComponent', () => {
  let shown: string[];
  let disposed: number;
  let scene: SpartanScene;

  beforeEach(() => {
    shown = [];
    disposed = 0;
    scene = { show: async url => { shown.push(url); }, dispose: () => { disposed++; } };
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

describe('createSpartanScene', () => {
  const webgl = (() => { try { return !!document.createElement('canvas').getContext('webgl2'); } catch { return false; } })();

  // A one-triangle glTF, as a data address
  const triangle = (() => {
    const bytes = new Uint8Array(new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]).buffer);
    const gltf = {
      asset: { version: '2.0' }, scene: 0, scenes: [{ nodes: [0] }], nodes: [{ mesh: 0 }],
      meshes: [{ primitives: [{ attributes: { POSITION: 0 } }] }],
      accessors: [{ bufferView: 0, componentType: 5126, count: 3, type: 'VEC3', min: [0, 0, 0], max: [1, 1, 0] }],
      bufferViews: [{ buffer: 0, byteLength: 36 }],
      buffers: [{ byteLength: 36, uri: `data:application/octet-stream;base64,${btoa(String.fromCharCode(...bytes))}` }],
    };
    return `data:model/gltf+json;base64,${btoa(JSON.stringify(gltf))}`;
  })();

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
  });

  it('refuses a model that is not one', async () => {
    if (!webgl) { pending('This browser has no WebGL.'); return; }
    const canvas = document.createElement('canvas');
    const scene = await createSpartanScene(canvas);

    await expectAsync(scene.show('data:model/gltf+json;base64,e30=')).toBeRejected();
    scene.dispose();
  });
});
