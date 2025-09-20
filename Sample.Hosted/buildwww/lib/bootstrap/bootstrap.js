import Alert from '../../../node_modules/bootstrap/js/dist/alert'
import Button from '../../../node_modules/bootstrap/js/dist/button'
import Modal from '../../../node_modules/bootstrap/js/dist/modal'
import Tooltip from '../../../node_modules/bootstrap/js/dist/tooltip'
import Toast from '../../../node_modules/bootstrap/js/dist/toast'
import Collapse from '../../../node_modules/bootstrap/js/dist/collapse'
import Dropdown from '../../../node_modules/bootstrap/js/dist/dropdown'
import Tab from '../../../node_modules/bootstrap/js/dist/tab'
import Carousel from '../../../node_modules/bootstrap/js/dist/carousel'
import Popover from '../../../node_modules/bootstrap/js/dist/popover'
import Offcanvas from '../../../node_modules/bootstrap/js/dist/offcanvas'
import ScrollSpy from '../../../node_modules/bootstrap/js/dist/scrollspy'

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