/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-13.js
 * @description Object.isFrozen - 'O' is a Function object
 */


function testcase() {

        var obj = function () { };
        
        Object.defineProperty(obj, "property", {
            value: 12,
            writable: true,
            configurable: false
        });

        Object.preventExtensions(obj);

        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
