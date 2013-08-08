/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-12.js
 * @description Object.isFrozen - 'O' is a String object
 */


function testcase() {

        var obj = new String("abc");

        obj.len = 100;

        Object.preventExtensions(obj);

        return !Object.isFrozen(obj);
    }
runTestCase(testcase);
