import Alert from 'bootstrap/js/dist/alert'
import Button from 'bootstrap/js/dist/button'
import Modal from 'bootstrap/js/dist/modal'
import Tooltip from 'bootstrap/js/dist/tooltip'
import Toast from 'bootstrap/js/dist/toast'
import Collapse from 'bootstrap/js/dist/collapse'
import Dropdown from 'bootstrap/js/dist/dropdown'
import Tab from 'bootstrap/js/dist/tab'
import Carousel from 'bootstrap/js/dist/carousel'
import Popover from 'bootstrap/js/dist/popover'
import Offcanvas from 'bootstrap/js/dist/offcanvas'
import ScrollSpy from 'bootstrap/js/dist/scrollspy'

// Create and export the bootstrap object
const bootstrap = {
    Alert,
    Button,
    Modal,
    Tooltip,
    Toast,
    Collapse,
    Dropdown,
    Tab,
    Carousel,
    Popover,
    Offcanvas,
    ScrollSpy
}

// Make it globally available
if (typeof window !== 'undefined') {
    window.bootstrap = bootstrap
}

export { bootstrap }