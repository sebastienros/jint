/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.2/15.3.2.1/15.3.2.1-11-9-s.js
 * @description Strict Mode - SyntaxError is thrown if a function is created using the Function constructor that has three identical parameters and there is no explicit 'use strict' in the function constructor's body
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var foo = new Function("baz", "baz", "baz", "return 0;");
        return true;
    }
runTestCase(testcase);
