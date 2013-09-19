/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-29-s.js
 * @description Strict Mode - The built-in Function constructor is contained in use strict code
 * @noStrict
 */


function testcase() {
        "use strict";
        var funObj = new Function("a", "eval('public = 1;');");
        funObj();
        return true;
    }
runTestCase(testcase);
