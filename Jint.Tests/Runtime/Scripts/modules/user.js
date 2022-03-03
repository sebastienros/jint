import { formatName as nameFormatter } from './format-name.js';

class User {
    constructor(firstName, lastName) {
        this._firstName = firstName;
        this._lastName = lastName;
    }

    get name() {
        return nameFormatter(this._firstName, this._lastName);
    }
}

export { User };
