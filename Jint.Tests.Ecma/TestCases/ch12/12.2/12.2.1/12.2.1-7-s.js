/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-7-s.js
 * @description eval - a direct eval declaring a var named 'eval' throws SyntaxError in strict mode
 * @onlyStrict
 */


function testcase() {
  'use strict';

  try {
    eval('var eval;');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
