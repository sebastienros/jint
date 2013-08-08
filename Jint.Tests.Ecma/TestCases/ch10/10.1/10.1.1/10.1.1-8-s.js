/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-8-s.js
 * @description Strict Mode - Use Strict Directive Prologue is ''use strict';' which appears twice in the directive prologue
 * @noStrict
 */


function testcase() {
        "use strict";
        "use strict";
        try {
            eval("var public = 1;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
