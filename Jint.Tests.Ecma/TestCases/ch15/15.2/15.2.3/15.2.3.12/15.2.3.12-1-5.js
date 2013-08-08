/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-5.js
 * @description Object.isFrozen applies to dense array
 */


function testcase() {
        var obj = Object.freeze([0, 1, 2]);
        return Object.isFrozen(obj);
    }
runTestCase(testcase);
