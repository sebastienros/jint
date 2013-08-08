/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-9.js
 * @description Object.seal - 'O' is an Arguments object
 */


function testcase() {

        var argObj = (function () { return arguments; })();

        var preCheck = Object.isExtensible(argObj);
        Object.seal(argObj);

        return preCheck && Object.isSealed(argObj);

    }
runTestCase(testcase);
