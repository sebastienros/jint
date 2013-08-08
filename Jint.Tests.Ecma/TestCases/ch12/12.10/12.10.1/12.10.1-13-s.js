/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-13-s.js
 * @description Strict Mode - SyntaxError isn't thrown when WithStatement body is in strict mode code
 * @noStrict
 */


function testcase() {
        with ({}) {
            "use strict";
        }
        return true;
    }
runTestCase(testcase);
