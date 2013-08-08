/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-12.js
 * @description with introduces scope - name lookup finds property
 */


function testcase() {
  function f(o) {

    function innerf(o) {
      with (o) {
        return x;
      }
    }

    return innerf(o);
  }
  
  if (f({x:42}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
