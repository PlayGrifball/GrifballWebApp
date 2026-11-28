import { InjectionToken } from '@angular/core';

/** A 3D view of one model in a canvas: show a model by its address, pose it with one of its clips, and let go of everything the view holds. */
export interface SpartanScene {
  /** Shows the model and gives the names of its clips (none when a newer model, or disposing, overtook it). */
  show(url: string): Promise<readonly string[]>;
  /** Poses the model with the clip of that name, now or once the model has loaded; a model without the clip stays as it is. */
  play(clip: string): void;
  dispose(): void;
}

export type SpartanSceneFactory = (canvas: HTMLCanvasElement) => Promise<SpartanScene>;

/** Takes a model out of the scene and frees its geometry, materials and textures. */
export const releaseModel = (
  THREE: typeof import('three'),
  scene: InstanceType<typeof import('three').Scene>,
  group: InstanceType<typeof import('three').Group>,
): void => {
  scene.remove(group);
  group.traverse(object => {
    if (!(object instanceof THREE.Mesh)) return;
    object.geometry.dispose();
    for (const material of Array.isArray(object.material) ? object.material : [object.material]) {
      for (const value of Object.values(material)) if (value instanceof THREE.Texture) value.dispose();
      material.dispose();
    }
  });
};

/** three.js is loaded here, on first use, so only a page that shows a Spartan in 3D pays for it. */
export const createSpartanScene: SpartanSceneFactory = async canvas => {
  const [THREE, { GLTFLoader }, { OrbitControls }, { RoomEnvironment }] = await Promise.all([
    import('three'),
    import('three/examples/jsm/loaders/GLTFLoader.js'),
    import('three/examples/jsm/controls/OrbitControls.js'),
    import('three/examples/jsm/environments/RoomEnvironment.js'),
  ]);

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  const scene = new THREE.Scene();
  const environment = new THREE.PMREMGenerator(renderer);
  const environmentMap = environment.fromScene(new RoomEnvironment(), 0.04);
  scene.environment = environmentMap.texture;
  const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 100);
  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.enablePan = false;
  controls.autoRotate = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  controls.autoRotateSpeed = 1.5;
  const loader = new GLTFLoader();
  let model: InstanceType<typeof THREE.Group> | null = null;
  let mixer: InstanceType<typeof THREE.AnimationMixer> | null = null;
  let clips: InstanceType<typeof THREE.AnimationClip>[] = [];
  let wanted: string | null = null;
  let latest = 0;
  let disposed = false;

  const pose = () => {
    const clip = mixer && clips.find(c => c.name === wanted);
    if (!mixer || !clip) return;
    mixer.stopAllAction();
    mixer.clipAction(clip).play();
    mixer.update(0);
  };
  const resize = () => {
    const { clientWidth: width, clientHeight: height } = canvas;
    if (width === 0 || height === 0) return;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  const observer = new ResizeObserver(resize);
  observer.observe(canvas);
  resize();
  renderer.setAnimationLoop(() => {
    controls.update();
    renderer.render(scene, camera);
  });

  return {
    async show(url) {
      const mine = ++latest;
      const gltf = await loader.loadAsync(url);
      if (disposed || mine !== latest) {
        releaseModel(THREE, scene, gltf.scene);
        return [];
      }
      if (model) releaseModel(THREE, scene, model);
      model = gltf.scene;
      mixer = new THREE.AnimationMixer(model);
      clips = gltf.animations;
      scene.add(model);
      pose();
      const box = new THREE.Box3().setFromObject(model);
      const size = box.getSize(new THREE.Vector3());
      const center = box.getCenter(new THREE.Vector3());
      const distance = (Math.max(size.y, size.x / camera.aspect) / 2) / Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * 1.15;
      camera.position.set(center.x, center.y, center.z + distance + size.z / 2);
      controls.target.copy(center);
      controls.update();
      return clips.map(c => c.name);
    },
    play(clip) {
      wanted = clip;
      pose();
    },
    dispose() {
      disposed = true;
      renderer.setAnimationLoop(null);
      observer.disconnect();
      controls.dispose();
      if (model) releaseModel(THREE, scene, model);
      environmentMap.dispose();
      environment.dispose();
      renderer.dispose();
    },
  };
};

export const SPARTAN_SCENE = new InjectionToken<SpartanSceneFactory>('SPARTAN_SCENE', {
  providedIn: 'root',
  factory: () => createSpartanScene,
});
