/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.1/10.1.1/10.1.1-7-s.js
 * @description Strict Mode - Use Strict Directive Prologue is ''use strict';' which appears at the end of the block
 * @noStrict
 */


function testcase() {
        var public = 1;
        return public === 1;
        "use strict";
    }
runTestCase(testcase);
