/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-8.js
 * @description Object.seal - 'O' is an Error object
 */


function testcase() {

        var errObj = new Error();
        var preCheck = Object.isExtensible(errObj);
        Object.seal(errObj);

        return preCheck && Object.isSealed(errObj);

    }
runTestCase(testcase);
