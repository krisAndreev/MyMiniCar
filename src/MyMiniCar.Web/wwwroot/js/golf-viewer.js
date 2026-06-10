// Golf 4 3D preview — Three.js GLTF viewer with drag-orbit and live body recolour.
// Loaded as an ES module from Blazor via import(). One viewer instance per init() call.

import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

function showOverlay(container, msg) {
    const el = document.createElement('div');
    el.className = 'golf-overlay';
    el.textContent = msg;
    container.appendChild(el);
    return el;
}

function webglSupported() {
    try {
        const c = document.createElement('canvas');
        return !!(c.getContext('webgl2') || c.getContext('webgl'));
    } catch {
        return false;
    }
}

export function createViewer(container, modelUrl) {
    if (!webglSupported()) {
        showOverlay(container, 'WebGL is not available in this browser. Enable hardware acceleration (or update your browser) to see the 3D preview.');
        return { setColor() {}, setMaterial() {}, dispose() {} };
    }

    const state = {
        container,
        renderer: null,
        scene: null,
        camera: null,
        controls: null,
        bodyMaterial: null,
        raf: 0,
        resizeObserver: null,
        disposed: false,
    };

    const width = container.clientWidth || 600;
    const height = container.clientHeight || 400;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(width, height);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    container.appendChild(renderer.domElement);
    state.renderer = renderer;

    const scene = new THREE.Scene();
    state.scene = scene;

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    state.camera = camera;

    // Studio lighting: soft ambient + key/fill so paint reads cleanly.
    scene.add(new THREE.AmbientLight(0xffffff, 0.9));
    const key = new THREE.DirectionalLight(0xffffff, 1.4);
    key.position.set(5, 8, 6);
    scene.add(key);
    const fill = new THREE.DirectionalLight(0xffffff, 0.6);
    fill.position.set(-6, 3, -4);
    scene.add(fill);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.enablePan = false;
    controls.minDistance = 2;
    controls.maxDistance = 20;
    state.controls = controls;

    const loader = new GLTFLoader();
    loader.load(
        modelUrl,
        (gltf) => {
            if (state.disposed) return;
            const model = gltf.scene;

            // Centre the model at origin and frame the camera to fit it.
            const box = new THREE.Box3().setFromObject(model);
            const size = box.getSize(new THREE.Vector3());
            const center = box.getCenter(new THREE.Vector3());
            model.position.sub(center);

            // The glTF declares every material as BLEND + double-sided, which makes
            // the renderer depth-sort per angle and show the interior/backfaces
            // through the bodywork. Force opaque, single-sided (cull backfaces) so
            // only the solid exterior is ever visible, regardless of rotation.
            let bestVol = -1;
            const meshes = [];
            model.traverse((o) => {
                if (!o.isMesh) return;
                const mats = Array.isArray(o.material) ? o.material : [o.material];
                mats.forEach((m) => {
                    m.transparent = false;
                    m.depthWrite = true;
                    m.alphaTest = 0;
                    m.side = THREE.FrontSide;
                    m.needsUpdate = true;
                });

                // Pick the body: the mesh with the largest bounding-box volume
                // (exterior shell encloses the most space; wheels/glass are smaller).
                const b = new THREE.Box3().setFromObject(o);
                const s = b.getSize(new THREE.Vector3());
                const vol = s.x * s.y * s.z;
                meshes.push({ name: o.name, mat: o.material?.name, vol: vol.toFixed(3) });
                if (vol > bestVol) {
                    bestVol = vol;
                    state.bodyMaterial = o.material;
                }
            });
            console.log('[golf-viewer] meshes:', meshes);
            console.log('[golf-viewer] body material:', state.bodyMaterial?.name);

            scene.add(model);

            // Frame the camera to the bounding SPHERE so the car never clips at any
            // rotation angle (the longest axis always fits). Smaller margin = bigger.
            const radius = Math.max(size.x, size.y, size.z) * 0.5 * Math.sqrt(2);
            const margin = 1.18;
            const dist = (radius * margin) / Math.sin((Math.PI * camera.fov) / 360);
            camera.position.set(dist * 0.62, dist * 0.42, dist * 0.78);
            controls.target.set(0, 0, 0);
            controls.minDistance = dist * 0.5;
            controls.maxDistance = dist * 2.5;
            controls.update();
        },
        undefined,
        (err) => {
            console.error('[golf-viewer] load failed:', err);
            showOverlay(container, 'Could not load the 3D model. Check your connection and reload.');
        }
    );

    const animate = () => {
        if (state.disposed) return;
        state.raf = requestAnimationFrame(animate);
        controls.update();
        renderer.render(scene, camera);
    };
    animate();

    const onResize = () => {
        if (state.disposed) return;
        const w = container.clientWidth || width;
        const h = container.clientHeight || height;
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    };
    state.resizeObserver = new ResizeObserver(onResize);
    state.resizeObserver.observe(container);

    return {
        // Live recolour of the body paint. Dropping the diffuse map gives a clean,
        // saturated paint colour instead of tinting the baked texture.
        setColor(hex) {
            const mat = state.bodyMaterial;
            if (!mat) return;
            mat.map = null;
            mat.color = new THREE.Color(hex);
            mat.metalness = 0.35;
            mat.roughness = 0.45;
            mat.needsUpdate = true;
        },
        // Apply a finish: effective colour + PBR params. `emissive` makes the body
        // glow in the chosen colour (glow-in-the-dark finish).
        setMaterial({ color, metalness, roughness, emissive }) {
            const mat = state.bodyMaterial;
            if (!mat) return;
            mat.map = null;
            mat.color = new THREE.Color(color);
            mat.metalness = metalness;
            mat.roughness = roughness;
            mat.emissive = emissive ? new THREE.Color(color) : new THREE.Color(0x000000);
            mat.needsUpdate = true;
        },
        dispose() {
            state.disposed = true;
            cancelAnimationFrame(state.raf);
            state.resizeObserver?.disconnect();
            state.controls?.dispose();
            state.scene?.traverse((o) => {
                if (o.isMesh) {
                    o.geometry?.dispose();
                    const mats = Array.isArray(o.material) ? o.material : [o.material];
                    mats.forEach((m) => m?.dispose());
                }
            });
            state.renderer?.dispose();
            if (state.renderer?.domElement?.parentNode === container) {
                container.removeChild(state.renderer.domElement);
            }
        },
    };
}
