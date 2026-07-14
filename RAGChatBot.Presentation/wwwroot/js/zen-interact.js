window.zenInteract = {
    initLenis: function () {
        if (typeof Lenis === 'undefined' || typeof gsap === 'undefined') return;

        // Tự động tắt Lenis nếu người dùng bật chế độ giảm hoạt ảnh (Accessibility)
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            document.documentElement.style.scrollBehavior = 'auto';
            return;
        }

        if (window.lenis) {
            window.lenis.destroy();
        }

        window.lenis = new Lenis({
            duration: 1.2,
            easing: (t) => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
            orientation: 'vertical',
            gestureOrientation: 'vertical',
            smoothWheel: true,
            wheelMultiplier: 1,
            smoothTouch: false,
            touchMultiplier: 2,
            infinite: false,
        });

        if (typeof ScrollTrigger !== 'undefined') {
            window.lenis.on('scroll', ScrollTrigger.update);
        }

        gsap.ticker.add((time) => {
            window.lenis.raf(time * 1000);
        });

        gsap.ticker.lagSmoothing(0);
    },

    initPageReveal: function () {
        if (typeof gsap === 'undefined') {
            document.querySelectorAll('.gsap-fade-up, .zen-list-item').forEach(element => {
                element.style.opacity = '1';
                element.style.transform = 'none';
            });
            return;
        }

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (prefersReducedMotion) {
            gsap.set(".gsap-fade-up, .zen-list-item", { y: 0, opacity: 1 });
            return;
        }

        gsap.set(".gsap-fade-up", { y: 30, opacity: 0 });
        gsap.to(".gsap-fade-up", { 
            y: 0, 
            opacity: 1, 
            duration: 1.2, 
            stagger: 0.1,
            ease: "power3.out" 
        });

        gsap.set(".zen-list-item", { y: 20, opacity: 0 });
        gsap.to(".zen-list-item", {
            y: 0,
            opacity: 1,
            duration: 1.0,
            stagger: 0.1,
            ease: "power2.out",
            delay: 0.2
        });
    },

    initMagneticButtons: function () {
        if (typeof gsap === 'undefined') return;

        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const magnets = document.querySelectorAll('.magnetic-btn');
        magnets.forEach(magnet => {
            if (magnet.dataset.magneticInit) return;
            magnet.dataset.magneticInit = 'true';

            magnet.addEventListener('mousemove', function(e) {
                const position = magnet.getBoundingClientRect();
                const x = e.clientX - position.left - position.width / 2;
                const y = e.clientY - position.top - position.height / 2;

                gsap.to(magnet, {
                    x: x * 0.3,
                    y: y * 0.3,
                    duration: 0.6,
                    ease: "power3.out"
                });
            });

            magnet.addEventListener('mouseleave', function() {
                gsap.to(magnet, {
                    x: 0,
                    y: 0,
                    duration: 0.6,
                    ease: "power3.out"
                });
            });
        });
    },

    showToast: function (message, type = 'success') {
        const container = document.getElementById('zen-toast-container') || this.createToastContainer();
        
        const toast = document.createElement('div');
        toast.className = `zen-toast zen-toast-${type}`;
        
        const icon = type === 'success' ? 'fa-check' : 'fa-exclamation';
        const iconElement = document.createElement('i');
        iconElement.className = `fa-solid ${icon} me-2`;
        const messageElement = document.createElement('span');
        messageElement.textContent = String(message ?? '');
        toast.append(iconElement, messageElement);
        
        container.appendChild(toast);

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const canAnimate = typeof gsap !== 'undefined' && !prefersReducedMotion;
        if (!canAnimate) {
            toast.style.opacity = 1;
            toast.style.transform = 'none';
        } else {
            gsap.fromTo(toast, 
                { y: 50, opacity: 0 }, 
                { y: 0, opacity: 1, duration: 0.6, ease: "power3.out" }
            );
        }

        setTimeout(() => {
            if (!canAnimate) {
                toast.remove();
            } else {
                gsap.to(toast, {
                    y: -20,
                    opacity: 0,
                    duration: 0.6,
                    ease: "power2.in",
                    onComplete: () => {
                        toast.remove();
                    }
                });
            }
        }, 3000);
    },

    createToastContainer: function () {
        const container = document.createElement('div');
        container.id = 'zen-toast-container';
        document.body.appendChild(container);
        return container;
    },

    initChatDrawer: function () {
        const toggleBtn = document.querySelector('.zen-drawer-toggle');
        const closeBtn = document.querySelector('.zen-drawer-close');
        const drawer = document.querySelector('.zen-chat-drawer');
        if (!toggleBtn || !drawer) return;

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const canAnimate = typeof gsap !== 'undefined' && !prefersReducedMotion;
        const drawerWidth = () => Math.min(320, Math.max(0, window.innerWidth - 24));

        const updateAccessibility = isOpen => {
            toggleBtn.setAttribute('aria-expanded', String(isOpen));
            drawer.setAttribute('aria-hidden', String(!isOpen));
        };

        const finishWithoutAnimation = isOpen => {
            drawer.classList.toggle('active', isOpen);
            drawer.style.removeProperty('width');
            drawer.style.removeProperty('border-right');
            updateAccessibility(isOpen);
        };

        const open = () => {
            if (drawer.classList.contains('active')) return;
            drawer.classList.add('active');
            updateAccessibility(true);

            if (!canAnimate) {
                finishWithoutAnimation(true);
                return;
            }

            gsap.killTweensOf(drawer);
            gsap.fromTo(drawer,
                { width: 0, borderRightWidth: 0 },
                {
                    width: drawerWidth(),
                    borderRightWidth: 1,
                    duration: 0.6,
                    ease: 'power3.out',
                    onComplete: () => {
                        drawer.style.removeProperty('width');
                        drawer.style.removeProperty('border-right-width');
                    }
                }
            );

            const threadItems = drawer.querySelectorAll('.zen-thread-item');
            if (threadItems.length > 0) {
                gsap.fromTo(threadItems,
                    { opacity: 0, x: -20 },
                    { opacity: 1, x: 0, duration: 0.4, stagger: 0.05, ease: 'power2.out', delay: 0.1 }
                );
            }
        };

        const close = () => {
            if (!drawer.classList.contains('active')) return;

            if (!canAnimate) {
                finishWithoutAnimation(false);
                return;
            }

            gsap.killTweensOf(drawer);
            gsap.to(drawer, {
                width: 0,
                borderRightWidth: 0,
                duration: 0.5,
                ease: 'power3.inOut',
                onComplete: () => finishWithoutAnimation(false)
            });
        };

        updateAccessibility(false);
        toggleBtn.addEventListener('click', () => {
            drawer.classList.contains('active') ? close() : open();
        });
        if (closeBtn) {
            closeBtn.addEventListener('click', close);
        }

        document.addEventListener('keydown', event => {
            if (event.key === 'Escape') close();
        });

        document.addEventListener('click', event => {
            if (window.innerWidth > 768 || !drawer.classList.contains('active')) return;
            if (!drawer.contains(event.target) && !toggleBtn.contains(event.target)) close();
        });
    },

    initWashiSkeleton: function () {
        const dots = document.querySelectorAll('.washi-ink-dot');
        if (dots.length === 0) return;

        // Vô hiệu hóa hoạt ảnh nếu prefers-reduced-motion bật
        if (typeof gsap === 'undefined' || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            dots.forEach(dot => { dot.style.opacity = '0.6'; });
            return;
        }

        gsap.to(dots, {
            y: -6,
            opacity: 1,
            duration: 0.6,
            stagger: {
                each: 0.2,
                repeat: -1,
                yoyo: true
            },
            ease: "power1.inOut"
        });
    },

    init3DTilt: function () {
        if (typeof gsap === 'undefined') return;
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const configs = [
            { selector: '.zen-suggestion-item', max: 8 },
            { selector: '.hanko-seal', max: 12 },
            { selector: '.btn-zen', max: 10 },
            { selector: '.zen-input-area', max: 4 }
        ];

        configs.forEach(config => {
            const elements = document.querySelectorAll(config.selector);
            elements.forEach(el => {
                // Thiết lập 3D perspective trên cha và transform-style preserve-3d trên đối tượng
                gsap.set(el.parentElement, { perspective: 1000 });
                gsap.set(el, { transformStyle: "preserve-3d", force3D: true });

                // Khởi tạo các hàm quickTo để tái sử dụng, tránh tạo tween mới trên mỗi sự kiện mousemove
                const rotXTo = gsap.quickTo(el, "rotationX", { duration: 0.3, ease: "power2.out" });
                const rotYTo = gsap.quickTo(el, "rotationY", { duration: 0.3, ease: "power2.out" });

                el.addEventListener('mousemove', (e) => {
                    const bounds = el.getBoundingClientRect();
                    const x = e.clientX - bounds.left;
                    const y = e.clientY - bounds.top;
                    
                    // Tính tọa độ lệch tâm (-0.5 đến 0.5)
                    const relX = (x / bounds.width) - 0.5;
                    const relY = (y / bounds.height) - 0.5;

                    const rotX = -relY * config.max;
                    const rotY = relX * config.max;

                    rotXTo(rotX);
                    rotYTo(rotY);
                });

                el.addEventListener('mouseleave', () => {
                    gsap.to(el, {
                        rotationX: 0,
                        rotationY: 0,
                        duration: 0.6,
                        ease: "power3.out",
                        overwrite: "auto"
                    });
                });
            });
        });
    },

    init3DScroll: function () {
        if (typeof gsap === 'undefined' || typeof ScrollTrigger === 'undefined') return;
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        gsap.registerPlugin(ScrollTrigger);

        // Chỉ animate các section độc lập. Không chọn container cha của các section
        // để tránh nhiều ScrollTrigger cùng điều khiển transform/opacity của cây chat.
        const targets = ['.zen-hero', '.zen-chat-layout', '.zen-container.py-5'];
        const elements = Array.from(document.querySelectorAll(targets.join(', ')))
            .filter(element => !element.classList.contains('gsap-fade-up'));
        
        elements.forEach(el => {
            // Kích hoạt transform 3D và tối ưu GPU
            el.classList.add('zen-scroll-flip');

            // Tạo timeline lật 3D cuốn sách cổ
            const tl = gsap.timeline({
                scrollTrigger: {
                    trigger: el,
                    // Không clamp các section ở đầu trang về cùng mốc 0; start/end
                    // trùng nhau có thể giữ from-state (opacity: 0) trên trang chat.
                    start: "top bottom",
                    end: "top center",
                    scrub: 1                       // Scrub mượt mà trễ 1 giây
                }
            });

            tl.fromTo(el, 
                {
                    rotationX: -12,
                    y: 50,
                    opacity: 0,
                    transformOrigin: "top center"
                },
                {
                    rotationX: 0,
                    y: 0,
                    opacity: 1,
                    ease: "none"                   // Đặt ease: "none" ở đây để timeline đồng bộ 1:1 với tiến trình cuộn
                }
            );
        });
    },

    refreshScrollTriggers: function () {
        if (typeof ScrollTrigger !== 'undefined') {
            // Tự động dọn dẹp (kill) các ScrollTrigger mồ côi có phần tử trigger không còn nằm trong DOM
            ScrollTrigger.getAll().forEach(trigger => {
                if (trigger.trigger && !document.body.contains(trigger.trigger)) {
                    trigger.kill();
                }
            });
            ScrollTrigger.refresh();
        }
    }
};
