/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-2-3.js
 * @description Object.defineProperties - argument 'Properties' is a boolean whose value is false
 */


function testcase() {

        var obj = {};
        var obj1 = Object.defineProperties(obj, false);
        return obj === obj1;
    }
runTestCase(testcase);
