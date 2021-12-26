import { formatName as nameFormatter } from './module-b';

class User {
    constructor(name) {
        this._name = name;
    }


    get name() {
        return nameFormatter(this._name);
    }
}

export { User };