/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-c-1.js
 * @description Object.isFrozen returns false if 'O' contains own configurable data property
 */


function testcase() {

        var obj = {};
        Object.defineProperty(obj, "foo", {
            value: 20,
            writable: false,
            configurable: true
        });

        Object.preventExtensions(obj);
        return !Object.isFrozen(obj);

    }
runTestCase(testcase);
