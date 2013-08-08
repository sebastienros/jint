/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-9.js
 * @description Object.freeze - 'O' is the Arguments object
 */


function testcase() {
        var argObj = (function () { return arguments; } ());

        Object.freeze(argObj);

        return Object.isFrozen(argObj);

    }
runTestCase(testcase);
