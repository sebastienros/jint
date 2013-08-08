/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-3-1.js
 * @description Object.seal - returned object is not extensible
 */


function testcase() {

        var obj = {};
        var preCheck = Object.isExtensible(obj);
        Object.seal(obj);
        return preCheck && !Object.isExtensible(obj);

    }
runTestCase(testcase);
