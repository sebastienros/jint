/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-2.js
 * @description Object.seal - 'O' is an Array object
 */


function testcase() {

        var arr = [0, 1];
        var preCheck = Object.isExtensible(arr);
        Object.seal(arr);

        return preCheck && Object.isSealed(arr);

    }
runTestCase(testcase);
