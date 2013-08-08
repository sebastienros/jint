/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-8.js
 * @description Object.freeze - 'O' is an Error object
 */


function testcase() {
        var errObj = new SyntaxError();

        Object.freeze(errObj);

        return Object.isFrozen(errObj);
    }
runTestCase(testcase);
