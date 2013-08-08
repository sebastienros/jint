/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-3.js
 * @description Object.freeze - 'O' is a String object
 */


function testcase() {
        var strObj = new String("a");

        Object.freeze(strObj);

        return Object.isFrozen(strObj);
    }
runTestCase(testcase);
