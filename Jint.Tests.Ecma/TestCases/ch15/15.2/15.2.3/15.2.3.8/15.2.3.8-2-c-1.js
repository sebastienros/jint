/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-1.js
 * @description Object.seal - 'O' is a Function object
 */


function testcase() {

        var fun = function () { };
        var preCheck = Object.isExtensible(fun);
        Object.seal(fun);

        return preCheck && Object.isSealed(fun);

    }
runTestCase(testcase);
