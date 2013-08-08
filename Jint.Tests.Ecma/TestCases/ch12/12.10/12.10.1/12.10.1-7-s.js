/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-7-s.js
 * @description with statement in strict mode throws SyntaxError (function expression, where the container function is directly evaled from strict code)
 * @onlyStrict
 */


function testcase() {
  'use strict';

  try {
    eval("var f = function () {\
                var o = {}; \
                with (o) {}; \
             }\
        ");
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
