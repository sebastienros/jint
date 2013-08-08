/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-4-2.js
 * @description Object.freeze - 'O' is frozen already
 */


function testcase() {

        var obj = {};

        obj.foo = 10; // default value of attributes: writable: true, enumerable: true

        Object.freeze(obj);

        Object.freeze(obj);
        return Object.isFrozen(obj);

    }
runTestCase(testcase);
