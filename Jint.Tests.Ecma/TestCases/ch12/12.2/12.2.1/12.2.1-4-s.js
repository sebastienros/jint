/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-4-s.js
 * @description eval - a function expr assigning into 'eval' throws a SyntaxError in strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';

  try {
    eval('(function () { eval = 42; })()');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
