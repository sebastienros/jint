/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-1.js
 * @description Object.freeze - 'O' is a Function object
 */


function testcase() {
        var funObj = function () { };

        Object.freeze(funObj);

        return Object.isFrozen(funObj);
    }
runTestCase(testcase);
