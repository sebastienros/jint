/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-10-s.js
 * @description with statement in strict mode throws SyntaxError (eval, where the container function is strict)
 * @onlyStrict
 */


function testcase() {
  'use strict';
  
  // wrapping it in eval since this needs to be a syntax error. The
  // exception thrown must be a SyntaxError exception. Note that eval
  // inherits the strictness of its calling context.  
  try {
    eval("\
          var o = {};\
          with (o) {}\
       ");
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
