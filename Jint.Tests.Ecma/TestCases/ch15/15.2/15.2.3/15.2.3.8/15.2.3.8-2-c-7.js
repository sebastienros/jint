/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-7.js
 * @description Object.seal - 'O' is a RegExp object
 */


function testcase() {
        var regObj = new RegExp();
        var preCheck = Object.isExtensible(regObj);
        Object.seal(regObj);

        return preCheck && Object.isSealed(regObj);
    }
runTestCase(testcase);
