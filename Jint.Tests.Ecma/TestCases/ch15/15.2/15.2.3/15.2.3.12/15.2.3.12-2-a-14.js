/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-14.js
 * @description Object.isFrozen - 'O' is an Array object
 */


function testcase() {

        var obj = [2];
        obj.len = 200;

        Object.preventExtensions(obj);

        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
