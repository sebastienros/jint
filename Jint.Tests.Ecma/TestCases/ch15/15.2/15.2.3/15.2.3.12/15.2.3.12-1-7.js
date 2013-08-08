/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-1-7.js
 * @description Object.isFrozen applies to non-array object which contains index named properties
 */


function testcase() {
        var obj = Object.freeze({ 0: 0, 1: 1, 1000: 1000 });
        return Object.isFrozen(obj);
    }
runTestCase(testcase);
