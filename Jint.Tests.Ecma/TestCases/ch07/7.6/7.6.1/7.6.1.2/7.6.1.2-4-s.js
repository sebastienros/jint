/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1.2/7.6.1.2-4-s.js
 * @description Strict Mode - SyntaxError is thrown when FutureReservedWord 'public' occurs in strict mode code
 * @onlyStrict
 */




function testcase() {
        "use strict";

        try {
            eval("var public = 1;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
}
runTestCase(testcase);