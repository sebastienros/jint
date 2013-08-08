/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14-12.js
 * @description catch introduces scope - name lookup finds property
 */


function testcase() {
  function f(o) {

    function innerf(o) {
      try {
        throw o;
      }
      catch (e) {
        return e.x;
      }
    }

    return innerf(o);
  }
  
  if (f({x:42}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
